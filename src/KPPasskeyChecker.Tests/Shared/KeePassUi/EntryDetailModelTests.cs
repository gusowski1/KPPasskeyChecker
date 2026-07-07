using System.Collections.Generic;
using System.Drawing;
using KeeRadar.Shared.KeePassUi;
using Xunit;

namespace KPPasskeyChecker.Tests.Shared.KeePassUi
{
    /// <summary>
    /// Full public-surface coverage of <see cref="EntryDetailModel"/> — the parameterless
    /// constructor's defaults and every settable property, including the Rows collection and the
    /// nullable Banner/Window icon and EmptyMessage members. <see cref="EntryDetailModel"/> is a
    /// plain DTO (all plain get/set properties, no computed logic) — this suite pins the
    /// constructor defaults and each property's round-trip behaviour.
    /// Ownership: <c>KeeRadar.Shared.*</c> is tested exclusively in KPPasskeyChecker.Tests (the
    /// canonical source); KP2FAChecker.Tests excludes the whole namespace.
    /// </summary>
    public class EntryDetailModelTests
    {
        // --- Default constructor -------------------------------------------------------------------

        [Fact]
        public void Default_ctor_sets_Domain_to_empty_string()
        {
            EntryDetailModel model = new EntryDetailModel();

            Assert.Equal(string.Empty, model.Domain);
        }

        [Fact]
        public void Default_ctor_sets_BannerTitle_to_empty_string()
        {
            EntryDetailModel model = new EntryDetailModel();

            Assert.Equal(string.Empty, model.BannerTitle);
        }

        [Fact]
        public void Default_ctor_sets_Attribution_to_empty_string()
        {
            EntryDetailModel model = new EntryDetailModel();

            Assert.Equal(string.Empty, model.Attribution);
        }

        [Fact]
        public void Default_ctor_initializes_Rows_to_an_empty_non_null_list()
        {
            EntryDetailModel model = new EntryDetailModel();

            Assert.NotNull(model.Rows);
            Assert.Empty(model.Rows);
        }

        [Fact]
        public void Default_ctor_leaves_EmptyMessage_null()
        {
            EntryDetailModel model = new EntryDetailModel();

            Assert.Null(model.EmptyMessage);
        }

        [Fact]
        public void Default_ctor_leaves_BannerIcon_null()
        {
            EntryDetailModel model = new EntryDetailModel();

            Assert.Null(model.BannerIcon);
        }

        [Fact]
        public void Default_ctor_leaves_WindowIcon_null()
        {
            EntryDetailModel model = new EntryDetailModel();

            Assert.Null(model.WindowIcon);
        }

        // --- Property round-trip (plain get/set surface) -------------------------------------------

        [Fact]
        public void Domain_round_trips_when_set_explicitly()
        {
            EntryDetailModel model = new EntryDetailModel { Domain = "example.com" };

            Assert.Equal("example.com", model.Domain);
        }

        [Fact]
        public void Domain_can_be_set_to_null_overriding_the_constructor_default()
        {
            EntryDetailModel model = new EntryDetailModel();
            model.Domain = null;

            Assert.Null(model.Domain);
        }

        [Fact]
        public void BannerTitle_round_trips_when_set_explicitly()
        {
            EntryDetailModel model = new EntryDetailModel { BannerTitle = "Passkey Details" };

            Assert.Equal("Passkey Details", model.BannerTitle);
        }

        [Fact]
        public void Attribution_round_trips_when_set_explicitly()
        {
            EntryDetailModel model = new EntryDetailModel
            {
                Attribution = "Data sourced from Passkeys Directory by 2factorauth. (CC BY 4.0)"
            };

            Assert.Equal(
                "Data sourced from Passkeys Directory by 2factorauth. (CC BY 4.0)",
                model.Attribution);
        }

        [Fact]
        public void EmptyMessage_round_trips_when_set_explicitly()
        {
            EntryDetailModel model = new EntryDetailModel
            {
                EmptyMessage = "No data found for this domain in the directory."
            };

            Assert.Equal("No data found for this domain in the directory.", model.EmptyMessage);
        }

        [Fact]
        public void BannerIcon_round_trips_when_set_explicitly()
        {
            using (Image icon = new Bitmap(1, 1))
            {
                EntryDetailModel model = new EntryDetailModel { BannerIcon = icon };

                Assert.Same(icon, model.BannerIcon);
            }
        }

        [Fact]
        public void WindowIcon_round_trips_when_set_explicitly()
        {
            using (Icon icon = SystemIcons.Application)
            {
                EntryDetailModel model = new EntryDetailModel { WindowIcon = icon };

                Assert.Same(icon, model.WindowIcon);
            }
        }

        // --- Rows collection behaviour ---------------------------------------------------------------

        [Fact]
        public void Rows_can_be_populated_and_preserves_insertion_order()
        {
            EntryDetailModel model = new EntryDetailModel();
            TextDetailRow first = new TextDetailRow("A", "1");
            TextDetailRow second = new TextDetailRow("B", "2");

            model.Rows.Add(first);
            model.Rows.Add(second);

            Assert.Equal(2, model.Rows.Count);
            Assert.Same(first, model.Rows[0]);
            Assert.Same(second, model.Rows[1]);
        }

        [Fact]
        public void Rows_can_be_replaced_wholesale_via_the_setter()
        {
            EntryDetailModel model = new EntryDetailModel();
            IList<EntryDetailRow> replacement = new List<EntryDetailRow>
            {
                new NotesDetailRow("Notes", "text"),
            };

            model.Rows = replacement;

            Assert.Same(replacement, model.Rows);
            Assert.Single(model.Rows);
        }

        [Fact]
        public void Rows_can_be_set_to_null_overriding_the_constructor_default()
        {
            EntryDetailModel model = new EntryDetailModel();
            model.Rows = null;

            Assert.Null(model.Rows);
        }

        [Fact]
        public void Rows_can_hold_mixed_concrete_row_kinds()
        {
            EntryDetailModel model = new EntryDetailModel();
            model.Rows.Add(new TextDetailRow("Passwordless login", "Required"));
            model.Rows.Add(new LinkDetailRow("Documentation", "https://example.com/docs"));
            model.Rows.Add(new NotesDetailRow("Notes", "note"));

            Assert.Equal(3, model.Rows.Count);
            Assert.IsType<TextDetailRow>(model.Rows[0]);
            Assert.IsType<LinkDetailRow>(model.Rows[1]);
            Assert.IsType<NotesDetailRow>(model.Rows[2]);
        }
    }
}
