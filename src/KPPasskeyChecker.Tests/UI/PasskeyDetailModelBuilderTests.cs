using System.Collections.Generic;
using KPPasskeyChecker.Data;
using KPPasskeyChecker.UI;
using KeeRadar.Shared.KeePassUi;
using Xunit;

namespace KPPasskeyChecker.Tests.UI
{
    /// <summary>
    /// Full public-surface coverage of <see cref="PasskeyDetailModelBuilder"/> — <c>Build</c>,
    /// <c>FormatLevel</c> and <c>FormatRegions</c>, not just one touched member.
    /// <c>PasskeyDetailModelBuilder</c> is <c>internal</c>; visible
    /// here via the existing <c>InternalsVisibleTo("KPPasskeyChecker.Tests")</c>.
    /// </summary>
    public class PasskeyDetailModelBuilderTests
    {
        // --- Build: banner / attribution / domain -------------------------------------------------

        [Fact]
        public void Build_always_sets_domain_banner_title_and_attribution()
        {
            EntryDetailModel model = PasskeyDetailModelBuilder.Build("example.com", null);

            Assert.Equal("example.com", model.Domain);
            Assert.Equal("Passkey Details", model.BannerTitle);
            Assert.Equal(
                "Data sourced from Passkeys Directory by 2factorauth. (CC BY 4.0)",
                model.Attribution);
        }

        [Fact]
        public void Build_with_null_entry_leaves_EmptyMessage_null_so_the_form_shows_its_default()
        {
            EntryDetailModel model = PasskeyDetailModelBuilder.Build("example.com", null);

            Assert.Null(model.EmptyMessage);
        }

        [Fact]
        public void Build_with_null_entry_produces_no_rows()
        {
            EntryDetailModel model = PasskeyDetailModelBuilder.Build("example.com", null);

            Assert.Empty(model.Rows);
        }

        // --- Build: row presence per field (only present when the source field is present) -------

        [Fact]
        public void Build_with_an_entry_with_no_fields_set_produces_no_rows()
        {
            PasskeyEntry entry = new PasskeyEntry();

            EntryDetailModel model = PasskeyDetailModelBuilder.Build("example.com", entry);

            Assert.Empty(model.Rows);
        }

        [Fact]
        public void Build_adds_a_passwordless_row_only_when_Passwordless_has_a_value()
        {
            PasskeyEntry entry = new PasskeyEntry { Passwordless = PasskeySupportLevel.Required };

            EntryDetailModel model = PasskeyDetailModelBuilder.Build("example.com", entry);

            TextDetailRow row = Assert.IsType<TextDetailRow>(Assert.Single(model.Rows));
            Assert.Equal("Passwordless login", row.Label);
            Assert.Equal("Required", row.Value);
        }

        [Fact]
        public void Build_adds_an_mfa_row_only_when_Mfa_has_a_value()
        {
            PasskeyEntry entry = new PasskeyEntry { Mfa = PasskeySupportLevel.Allowed };

            EntryDetailModel model = PasskeyDetailModelBuilder.Build("example.com", entry);

            TextDetailRow row = Assert.IsType<TextDetailRow>(Assert.Single(model.Rows));
            Assert.Equal("As 2nd factor", row.Label);
            Assert.Equal("Allowed", row.Value);
        }

        [Fact]
        public void Build_adds_a_regions_row_only_when_regions_are_present()
        {
            PasskeyEntry entry = new PasskeyEntry { Regions = new[] { "us", "de" } };

            EntryDetailModel model = PasskeyDetailModelBuilder.Build("example.com", entry);

            TextDetailRow row = Assert.IsType<TextDetailRow>(Assert.Single(model.Rows));
            Assert.Equal("Regions", row.Label);
            Assert.Equal("US, DE", row.Value);
        }

        [Fact]
        public void Build_omits_the_regions_row_when_regions_is_empty()
        {
            PasskeyEntry entry = new PasskeyEntry { Regions = new string[0] };

            EntryDetailModel model = PasskeyDetailModelBuilder.Build("example.com", entry);

            Assert.Empty(model.Rows);
        }

        [Fact]
        public void Build_adds_a_documentation_link_row_only_when_the_url_is_present()
        {
            PasskeyEntry entry = new PasskeyEntry { DocumentationUrl = "https://example.com/docs" };

            EntryDetailModel model = PasskeyDetailModelBuilder.Build("example.com", entry);

            LinkDetailRow row = Assert.IsType<LinkDetailRow>(Assert.Single(model.Rows));
            Assert.Equal("Documentation", row.Label);
            Assert.Equal("https://example.com/docs", row.Url);
        }

        [Fact]
        public void Build_adds_a_recovery_link_row_only_when_the_url_is_present()
        {
            PasskeyEntry entry = new PasskeyEntry { RecoveryUrl = "https://example.com/recover" };

            EntryDetailModel model = PasskeyDetailModelBuilder.Build("example.com", entry);

            LinkDetailRow row = Assert.IsType<LinkDetailRow>(Assert.Single(model.Rows));
            Assert.Equal("Recovery", row.Label);
            Assert.Equal("https://example.com/recover", row.Url);
        }

        [Fact]
        public void Build_adds_a_notes_row_only_when_notes_are_present()
        {
            PasskeyEntry entry = new PasskeyEntry { Notes = "Some notes." };

            EntryDetailModel model = PasskeyDetailModelBuilder.Build("example.com", entry);

            NotesDetailRow row = Assert.IsType<NotesDetailRow>(Assert.Single(model.Rows));
            Assert.Equal("Notes", row.Label);
            Assert.Equal("Some notes.", row.Text);
        }

        [Fact]
        public void Build_orders_rows_passwordless_mfa_regions_documentation_recovery_notes()
        {
            PasskeyEntry entry = new PasskeyEntry
            {
                Passwordless = PasskeySupportLevel.Allowed,
                Mfa = PasskeySupportLevel.Required,
                Regions = new[] { "us" },
                DocumentationUrl = "https://example.com/docs",
                RecoveryUrl = "https://example.com/recover",
                Notes = "note",
            };

            EntryDetailModel model = PasskeyDetailModelBuilder.Build("example.com", entry);

            IList<EntryDetailRow> rows = model.Rows;
            Assert.Equal(6, rows.Count);
            Assert.Equal("Passwordless login", rows[0].Label);
            Assert.Equal("As 2nd factor", rows[1].Label);
            Assert.Equal("Regions", rows[2].Label);
            Assert.Equal("Documentation", rows[3].Label);
            Assert.Equal("Recovery", rows[4].Label);
            Assert.Equal("Notes", rows[5].Label);
        }

        // --- FormatLevel ----------------------------------------------------------------------------

        [Fact]
        public void FormatLevel_Required_maps_to_the_word_Required()
        {
            Assert.Equal("Required", PasskeyDetailModelBuilder.FormatLevel(PasskeySupportLevel.Required));
        }

        [Fact]
        public void FormatLevel_Allowed_maps_to_the_word_Allowed()
        {
            Assert.Equal("Allowed", PasskeyDetailModelBuilder.FormatLevel(PasskeySupportLevel.Allowed));
        }

        // --- FormatRegions --------------------------------------------------------------------------

        [Fact]
        public void FormatRegions_null_yields_empty_string()
        {
            Assert.Equal(string.Empty, PasskeyDetailModelBuilder.FormatRegions(null));
        }

        [Fact]
        public void FormatRegions_empty_list_yields_empty_string()
        {
            Assert.Equal(string.Empty, PasskeyDetailModelBuilder.FormatRegions(new string[0]));
        }

        [Fact]
        public void FormatRegions_positive_list_is_comma_joined_and_upper_cased()
        {
            Assert.Equal("US, DE", PasskeyDetailModelBuilder.FormatRegions(new[] { "us", "de" }));
        }

        [Fact]
        public void FormatRegions_leading_dash_marks_an_exclusion_list()
        {
            Assert.Equal(
                "All regions except: JP",
                PasskeyDetailModelBuilder.FormatRegions(new[] { "-jp" }));
        }

        [Fact]
        public void FormatRegions_exclusion_list_with_multiple_entries_joins_all_of_them()
        {
            Assert.Equal(
                "All regions except: JP, CN",
                PasskeyDetailModelBuilder.FormatRegions(new[] { "-jp", "-cn" }));
        }

        [Fact]
        public void FormatRegions_skips_null_and_empty_and_whitespace_only_entries()
        {
            Assert.Equal(
                "US, DE",
                PasskeyDetailModelBuilder.FormatRegions(new[] { "us", null, string.Empty, "   ", "de" }));
        }

        [Fact]
        public void FormatRegions_a_lone_dash_with_nothing_after_it_is_skipped()
        {
            Assert.Equal(string.Empty, PasskeyDetailModelBuilder.FormatRegions(new[] { "-" }));
        }

        [Fact]
        public void FormatRegions_trims_surrounding_whitespace_before_evaluating_the_leading_dash()
        {
            Assert.Equal(
                "All regions except: JP",
                PasskeyDetailModelBuilder.FormatRegions(new[] { "  -jp  " }));
        }
    }
}
