using KeeRadar.Shared.KeePassUi;
using Xunit;

namespace KPPasskeyChecker.Tests.Shared.KeePassUi
{
    /// <summary>
    /// Full public-surface coverage of <see cref="LinkDetailRow"/> — the single constructor, the
    /// inherited <see cref="EntryDetailRow"/> Label/AccessibleValue behaviour via the Url, and
    /// null/empty handling. Ownership: <c>KeeRadar.Shared.*</c> is tested exclusively in
    /// KPPasskeyChecker.Tests (the canonical source); KP2FAChecker.Tests excludes the whole
    /// namespace.
    /// </summary>
    public class LinkDetailRowTests
    {
        [Fact]
        public void Ctor_sets_Label_and_Url()
        {
            LinkDetailRow row = new LinkDetailRow("Documentation", "https://example.com/docs");

            Assert.Equal("Documentation", row.Label);
            Assert.Equal("https://example.com/docs", row.Url);
        }

        [Fact]
        public void Ctor_uses_url_as_AccessibleValue_when_url_is_non_empty()
        {
            LinkDetailRow row = new LinkDetailRow("Recovery", "https://example.com/recover");

            Assert.Equal("https://example.com/recover", row.AccessibleValue);
        }

        [Fact]
        public void Ctor_with_empty_url_falls_back_to_Label_as_AccessibleValue()
        {
            LinkDetailRow row = new LinkDetailRow("Recovery", string.Empty);

            Assert.Equal("Recovery", row.AccessibleValue);
        }

        [Fact]
        public void Ctor_with_null_url_stores_empty_string_and_falls_back_AccessibleValue_to_Label()
        {
            LinkDetailRow row = new LinkDetailRow("Recovery", null);

            Assert.Equal(string.Empty, row.Url);
            Assert.Equal("Recovery", row.AccessibleValue);
        }

        [Fact]
        public void Null_label_is_stored_as_empty_string()
        {
            LinkDetailRow row = new LinkDetailRow(null, "https://example.com");

            Assert.Equal(string.Empty, row.Label);
        }

        [Fact]
        public void Empty_label_and_a_present_url_still_yields_the_url_as_AccessibleValue()
        {
            LinkDetailRow row = new LinkDetailRow(string.Empty, "https://example.com");

            Assert.Equal(string.Empty, row.Label);
            Assert.Equal("https://example.com", row.AccessibleValue);
        }

        [Fact]
        public void Empty_label_and_empty_url_yields_empty_AccessibleValue()
        {
            LinkDetailRow row = new LinkDetailRow(string.Empty, string.Empty);

            Assert.Equal(string.Empty, row.Label);
            Assert.Equal(string.Empty, row.AccessibleValue);
        }
    }
}
