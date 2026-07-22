using KPPasskeyChecker.Data;
using Xunit;

namespace KPPasskeyChecker.Tests.Data
{
    /// <summary>
    /// Full public-surface coverage of <see cref="PasskeyEntry"/> — constructor defaults plus the
    /// three derived Supports* properties.
    /// </summary>
    public class PasskeyEntryTests
    {
        [Fact]
        public void Default_constructor_sets_PrimaryDomain_to_empty_string_not_null()
        {
            PasskeyEntry entry = new PasskeyEntry();
            Assert.Equal(string.Empty, entry.PrimaryDomain);
        }

        [Fact]
        public void Default_constructor_sets_AdditionalDomains_to_an_empty_list_not_null()
        {
            PasskeyEntry entry = new PasskeyEntry();
            Assert.NotNull(entry.AdditionalDomains);
            Assert.Empty(entry.AdditionalDomains);
        }

        [Fact]
        public void Default_constructor_sets_Regions_to_an_empty_list_not_null()
        {
            PasskeyEntry entry = new PasskeyEntry();
            Assert.NotNull(entry.Regions);
            Assert.Empty(entry.Regions);
        }

        [Fact]
        public void Default_constructor_leaves_Passwordless_and_Mfa_null()
        {
            PasskeyEntry entry = new PasskeyEntry();
            Assert.Null(entry.Passwordless);
            Assert.Null(entry.Mfa);
        }

        [Fact]
        public void Default_constructor_leaves_optional_string_and_reference_fields_null()
        {
            PasskeyEntry entry = new PasskeyEntry();
            Assert.Null(entry.DocumentationUrl);
            Assert.Null(entry.RecoveryUrl);
            Assert.Null(entry.Notes);
            Assert.Null(entry.Contact);
        }

        // --- SupportsPasswordless / SupportsMfa / SupportsAny -----------------------------------

        [Theory]
        [InlineData(null, false)]
        [InlineData(PasskeySupportLevel.Allowed, true)]
        [InlineData(PasskeySupportLevel.Required, true)]
        public void SupportsPasswordless_reflects_whether_Passwordless_has_a_value(
            PasskeySupportLevel? passwordless, bool expected)
        {
            PasskeyEntry entry = new PasskeyEntry { Passwordless = passwordless };
            Assert.Equal(expected, entry.SupportsPasswordless);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData(PasskeySupportLevel.Allowed, true)]
        [InlineData(PasskeySupportLevel.Required, true)]
        public void SupportsMfa_reflects_whether_Mfa_has_a_value(
            PasskeySupportLevel? mfa, bool expected)
        {
            PasskeyEntry entry = new PasskeyEntry { Mfa = mfa };
            Assert.Equal(expected, entry.SupportsMfa);
        }

        [Fact]
        public void SupportsAny_is_false_when_neither_passwordless_nor_mfa_is_set()
        {
            PasskeyEntry entry = new PasskeyEntry { Passwordless = null, Mfa = null };
            Assert.False(entry.SupportsAny);
        }

        [Fact]
        public void SupportsAny_is_true_when_only_passwordless_is_set()
        {
            PasskeyEntry entry = new PasskeyEntry { Passwordless = PasskeySupportLevel.Allowed, Mfa = null };
            Assert.True(entry.SupportsAny);
        }

        [Fact]
        public void SupportsAny_is_true_when_only_mfa_is_set()
        {
            PasskeyEntry entry = new PasskeyEntry { Passwordless = null, Mfa = PasskeySupportLevel.Required };
            Assert.True(entry.SupportsAny);
        }

        [Fact]
        public void SupportsAny_is_true_when_both_passwordless_and_mfa_are_set()
        {
            PasskeyEntry entry = new PasskeyEntry
            {
                Passwordless = PasskeySupportLevel.Allowed,
                Mfa = PasskeySupportLevel.Required
            };
            Assert.True(entry.SupportsAny);
        }

        // --- property setters round-trip (plain DTO behaviour) ----------------------------------

        [Fact]
        public void Properties_round_trip_the_assigned_values()
        {
            var contact = new ContactInfo { Email = "a@b.com" };
            var entry = new PasskeyEntry
            {
                PrimaryDomain = "example.com",
                AdditionalDomains = new[] { "example.net" },
                Passwordless = PasskeySupportLevel.Allowed,
                Mfa = PasskeySupportLevel.Required,
                DocumentationUrl = "https://example.com/docs",
                RecoveryUrl = "https://example.com/recover",
                Notes = "note",
                Contact = contact,
                Regions = new[] { "us" }
            };

            Assert.Equal("example.com", entry.PrimaryDomain);
            Assert.Equal(new[] { "example.net" }, entry.AdditionalDomains);
            Assert.Equal(PasskeySupportLevel.Allowed, entry.Passwordless);
            Assert.Equal(PasskeySupportLevel.Required, entry.Mfa);
            Assert.Equal("https://example.com/docs", entry.DocumentationUrl);
            Assert.Equal("https://example.com/recover", entry.RecoveryUrl);
            Assert.Equal("note", entry.Notes);
            Assert.Same(contact, entry.Contact);
            Assert.Equal(new[] { "us" }, entry.Regions);
        }
    }
}
