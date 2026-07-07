using System.Collections;
using System.Collections.Generic;
using KPPasskeyChecker.Data;
using Xunit;

namespace KPPasskeyChecker.Tests.Data
{
    /// <summary>
    /// Full public-surface coverage of <see cref="PasskeyEntryMapper"/>, covering the full logic
    /// surface, not just the touched member. Ported 1:1 from
    /// tools\SelfCheck\SelfCheck.cs (CheckSupportLevelParsing) and extended with the mapper
    /// assertions the CLAUDE.md "API schema notes" describe (regions/notes/documentation/recovery,
    /// contact/additional-domains forward-compat) that SelfCheck itself did not exercise.
    /// </summary>
    public class PasskeyEntryMapperTests
    {
        // --- mfa / passwordless parsing (ParseLevel via Map) ------------------------------------

        [Fact]
        public void Map_passwordless_allowed_parses_to_Allowed()
        {
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", Field("passwordless", "allowed"));
            Assert.Equal(PasskeySupportLevel.Allowed, entry.Passwordless);
        }

        [Fact]
        public void Map_mfa_required_parses_to_Required()
        {
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", Field("mfa", "required"));
            Assert.Equal(PasskeySupportLevel.Required, entry.Mfa);
        }

        [Fact]
        public void Map_missing_passwordless_field_yields_null()
        {
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", new Dictionary<string, object>());
            Assert.Null(entry.Passwordless);
        }

        [Fact]
        public void Map_missing_mfa_field_yields_null()
        {
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", new Dictionary<string, object>());
            Assert.Null(entry.Mfa);
        }

        [Fact]
        public void Map_invalid_support_level_value_yields_null_fail_safe()
        {
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", Field("mfa", "sometimes"));
            Assert.Null(entry.Mfa);
        }

        [Fact]
        public void Map_support_level_parsing_is_case_insensitive()
        {
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", Field("passwordless", "ReQuIrEd"));
            Assert.Equal(PasskeySupportLevel.Required, entry.Passwordless);
        }

        [Fact]
        public void Map_support_level_non_string_value_yields_null()
        {
            // GetString casts via "as string" — a non-string JSON value (e.g. a boxed number/bool)
            // must not throw and must be treated as absent.
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", Field("mfa", 42));
            Assert.Null(entry.Mfa);
        }

        // --- PrimaryDomain -----------------------------------------------------------------------

        [Fact]
        public void Map_sets_PrimaryDomain_from_the_domain_argument()
        {
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", new Dictionary<string, object>());
            Assert.Equal("example.com", entry.PrimaryDomain);
        }

        // --- simple string fields (documentation / recovery / notes) ----------------------------

        [Fact]
        public void Map_reads_documentation_url()
        {
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com",
                Field("documentation", "https://example.com/passkeys"));
            Assert.Equal("https://example.com/passkeys", entry.DocumentationUrl);
        }

        [Fact]
        public void Map_missing_documentation_url_yields_null()
        {
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", new Dictionary<string, object>());
            Assert.Null(entry.DocumentationUrl);
        }

        [Fact]
        public void Map_reads_recovery_url()
        {
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com",
                Field("recovery", "https://example.com/recover"));
            Assert.Equal("https://example.com/recover", entry.RecoveryUrl);
        }

        [Fact]
        public void Map_missing_recovery_url_yields_null()
        {
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", new Dictionary<string, object>());
            Assert.Null(entry.RecoveryUrl);
        }

        [Fact]
        public void Map_reads_notes()
        {
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com",
                Field("notes", "Only supported on mobile app."));
            Assert.Equal("Only supported on mobile app.", entry.Notes);
        }

        [Fact]
        public void Map_missing_notes_yields_null()
        {
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", new Dictionary<string, object>());
            Assert.Null(entry.Notes);
        }

        // --- regions (string list) --------------------------------------------------------------

        [Fact]
        public void Map_reads_regions_as_string_list()
        {
            var data = new Dictionary<string, object>
            {
                { "regions", MakeArrayList("us", "de") }
            };
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", data);

            Assert.Equal(new[] { "us", "de" }, entry.Regions);
        }

        [Fact]
        public void Map_regions_can_encode_exclusions_verbatim()
        {
            // CLAUDE.md: "regions values can be exclusions, e.g. ["-jp"]" — the mapper does not
            // interpret the leading '-', it is passed through as-is.
            var data = new Dictionary<string, object>
            {
                { "regions", MakeArrayList("-jp") }
            };
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", data);

            Assert.Equal(new[] { "-jp" }, entry.Regions);
        }

        [Fact]
        public void Map_missing_regions_yields_empty_list_not_null()
        {
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", new Dictionary<string, object>());
            Assert.NotNull(entry.Regions);
            Assert.Empty(entry.Regions);
        }

        [Fact]
        public void Map_regions_of_wrong_runtime_type_yields_empty_list()
        {
            // GetStringList casts via "as ArrayList" — a JSON value that isn't a JSON array (here
            // simulated as a plain string) must not throw and must degrade to empty.
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", Field("regions", "not-a-list"));
            Assert.NotNull(entry.Regions);
            Assert.Empty(entry.Regions);
        }

        [Fact]
        public void Map_regions_list_skips_non_string_and_null_and_empty_items()
        {
            var list = new ArrayList { "us", 123, null, string.Empty, "de" };
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", Field("regions", list));

            Assert.Equal(new[] { "us", "de" }, entry.Regions);
        }

        // --- additional-domains (forward-compat; not returned by the live v1 API today) ---------

        [Fact]
        public void Map_reads_additional_domains_as_string_list()
        {
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com",
                Field("additional-domains", MakeArrayList("example.org", "example.net")));

            Assert.Equal(new[] { "example.org", "example.net" }, entry.AdditionalDomains);
        }

        [Fact]
        public void Map_missing_additional_domains_yields_empty_list_not_null()
        {
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", new Dictionary<string, object>());
            Assert.NotNull(entry.AdditionalDomains);
            Assert.Empty(entry.AdditionalDomains);
        }

        // --- contact (nested object; forward-compat; not returned by the live v1 API today) -----

        [Fact]
        public void Map_missing_contact_yields_null()
        {
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", new Dictionary<string, object>());
            Assert.Null(entry.Contact);
        }

        [Fact]
        public void Map_contact_of_wrong_runtime_type_yields_null()
        {
            // MapContact casts via "as Dictionary<string, object>" — a JSON value that isn't a JSON
            // object (here simulated as a plain string) must not throw and must degrade to null.
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", Field("contact", "not-an-object"));
            Assert.Null(entry.Contact);
        }

        [Fact]
        public void Map_reads_full_contact_object()
        {
            var contact = new Dictionary<string, object>
            {
                { "twitter", "@example" },
                { "facebook", "example.page" },
                { "email", "support@example.com" },
                { "form", "https://example.com/contact" },
                { "language", "en" }
            };
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", Field("contact", contact));

            Assert.NotNull(entry.Contact);
            Assert.Equal("@example", entry.Contact.Twitter);
            Assert.Equal("example.page", entry.Contact.Facebook);
            Assert.Equal("support@example.com", entry.Contact.Email);
            Assert.Equal("https://example.com/contact", entry.Contact.Form);
            Assert.Equal("en", entry.Contact.Language);
        }

        [Fact]
        public void Map_contact_with_partial_fields_leaves_missing_ones_null()
        {
            var contact = new Dictionary<string, object>
            {
                { "email", "support@example.com" }
            };
            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", Field("contact", contact));

            Assert.NotNull(entry.Contact);
            Assert.Equal("support@example.com", entry.Contact.Email);
            Assert.Null(entry.Contact.Twitter);
            Assert.Null(entry.Contact.Facebook);
            Assert.Null(entry.Contact.Form);
            Assert.Null(entry.Contact.Language);
        }

        // --- full realistic entry (integration-style, all fields together) ----------------------

        [Fact]
        public void Map_a_fully_populated_entry_maps_every_field_correctly()
        {
            var contact = new Dictionary<string, object>
            {
                { "email", "support@example.com" }
            };
            var data = new Dictionary<string, object>
            {
                { "passwordless", "allowed" },
                { "mfa", "required" },
                { "documentation", "https://example.com/passkeys" },
                { "recovery", "https://example.com/recover" },
                { "notes", "Some notes." },
                { "regions", MakeArrayList("us", "-jp") },
                { "additional-domains", MakeArrayList("example.net") },
                { "contact", contact }
            };

            PasskeyEntry entry = PasskeyEntryMapper.Map("example.com", data);

            Assert.Equal("example.com", entry.PrimaryDomain);
            Assert.Equal(PasskeySupportLevel.Allowed, entry.Passwordless);
            Assert.Equal(PasskeySupportLevel.Required, entry.Mfa);
            Assert.Equal("https://example.com/passkeys", entry.DocumentationUrl);
            Assert.Equal("https://example.com/recover", entry.RecoveryUrl);
            Assert.Equal("Some notes.", entry.Notes);
            Assert.Equal(new[] { "us", "-jp" }, entry.Regions);
            Assert.Equal(new[] { "example.net" }, entry.AdditionalDomains);
            Assert.NotNull(entry.Contact);
            Assert.Equal("support@example.com", entry.Contact.Email);
        }

        // --- helpers -----------------------------------------------------------------------------

        private static Dictionary<string, object> Field(string key, object value)
        {
            return new Dictionary<string, object> { { key, value } };
        }

        private static ArrayList MakeArrayList(params string[] items)
        {
            var list = new ArrayList();
            foreach (string item in items)
                list.Add(item);
            return list;
        }
    }
}
