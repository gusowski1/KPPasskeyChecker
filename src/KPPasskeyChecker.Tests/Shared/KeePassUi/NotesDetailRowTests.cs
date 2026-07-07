using KeeRadar.Shared.KeePassUi;
using Xunit;

namespace KPPasskeyChecker.Tests.Shared.KeePassUi
{
    /// <summary>
    /// Full public-surface coverage of <see cref="NotesDetailRow"/> — the single constructor, the
    /// inherited <see cref="EntryDetailRow"/> Label/AccessibleValue behaviour via the Text, and
    /// null/empty handling. Ownership: <c>KeeRadar.Shared.*</c> is tested exclusively in
    /// KPPasskeyChecker.Tests (the canonical source); KP2FAChecker.Tests excludes the whole
    /// namespace.
    /// </summary>
    public class NotesDetailRowTests
    {
        [Fact]
        public void Ctor_sets_Label_and_Text()
        {
            NotesDetailRow row = new NotesDetailRow("Notes", "Some free text notes.");

            Assert.Equal("Notes", row.Label);
            Assert.Equal("Some free text notes.", row.Text);
        }

        [Fact]
        public void Ctor_uses_text_as_AccessibleValue_when_text_is_non_empty()
        {
            NotesDetailRow row = new NotesDetailRow("Notes", "Some free text notes.");

            Assert.Equal("Some free text notes.", row.AccessibleValue);
        }

        [Fact]
        public void Ctor_with_empty_text_falls_back_to_Label_as_AccessibleValue()
        {
            NotesDetailRow row = new NotesDetailRow("Notes", string.Empty);

            Assert.Equal("Notes", row.AccessibleValue);
        }

        [Fact]
        public void Ctor_with_null_text_stores_empty_string_and_falls_back_AccessibleValue_to_Label()
        {
            NotesDetailRow row = new NotesDetailRow("Notes", null);

            Assert.Equal(string.Empty, row.Text);
            Assert.Equal("Notes", row.AccessibleValue);
        }

        [Fact]
        public void Null_label_is_stored_as_empty_string()
        {
            NotesDetailRow row = new NotesDetailRow(null, "text");

            Assert.Equal(string.Empty, row.Label);
        }

        [Fact]
        public void Empty_label_and_a_present_text_still_yields_the_text_as_AccessibleValue()
        {
            NotesDetailRow row = new NotesDetailRow(string.Empty, "text");

            Assert.Equal(string.Empty, row.Label);
            Assert.Equal("text", row.AccessibleValue);
        }

        [Fact]
        public void Empty_label_and_empty_text_yields_empty_AccessibleValue()
        {
            NotesDetailRow row = new NotesDetailRow(string.Empty, string.Empty);

            Assert.Equal(string.Empty, row.Label);
            Assert.Equal(string.Empty, row.AccessibleValue);
        }

        [Fact]
        public void Multiline_text_is_preserved_verbatim()
        {
            string multiline = "Line one." + System.Environment.NewLine + "Line two.";

            NotesDetailRow row = new NotesDetailRow("Notes", multiline);

            Assert.Equal(multiline, row.Text);
        }
    }
}
