using KPPasskeyChecker.Data;
using Xunit;

namespace KPPasskeyChecker.Tests.Data
{
    /// <summary>
    /// Full public-surface coverage of <see cref="ContactInfo"/>. ContactInfo is a plain DTO with
    /// no derived logic — coverage here is constructor-default and property-round-trip, which is
    /// the entire surface there is.
    /// </summary>
    public class ContactInfoTests
    {
        [Fact]
        public void Default_instance_has_all_properties_null()
        {
            var contact = new ContactInfo();

            Assert.Null(contact.Twitter);
            Assert.Null(contact.Facebook);
            Assert.Null(contact.Email);
            Assert.Null(contact.Form);
            Assert.Null(contact.Language);
        }

        [Fact]
        public void Properties_round_trip_the_assigned_values()
        {
            var contact = new ContactInfo
            {
                Twitter = "@example",
                Facebook = "example.page",
                Email = "support@example.com",
                Form = "https://example.com/contact",
                Language = "en"
            };

            Assert.Equal("@example", contact.Twitter);
            Assert.Equal("example.page", contact.Facebook);
            Assert.Equal("support@example.com", contact.Email);
            Assert.Equal("https://example.com/contact", contact.Form);
            Assert.Equal("en", contact.Language);
        }

        [Fact]
        public void Properties_can_be_independently_set_to_empty_string()
        {
            // Distinguishes "field present but empty" from "field absent" (null) — the mapper
            // relies on this distinction being preserved verbatim by the DTO (no normalization).
            var contact = new ContactInfo { Email = string.Empty };

            Assert.Equal(string.Empty, contact.Email);
            Assert.Null(contact.Twitter);
        }
    }
}
