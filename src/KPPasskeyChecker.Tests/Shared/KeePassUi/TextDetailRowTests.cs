using KeeRadar.Shared.KeePassUi;
using Xunit;

namespace KPPasskeyChecker.Tests.Shared.KeePassUi
{
    /// <summary>
    /// Full public-surface coverage of <see cref="TextDetailRow"/> — both constructors, the
    /// inherited <see cref="EntryDetailRow"/> Label/AccessibleValue behaviour, and null/empty
    /// handling. <see cref="EntryDetailRow"/> itself is abstract and not directly instantiable; its
    /// behaviour is exercised here through its concrete subclass. Ownership:
    /// <c>KeeRadar.Shared.*</c> is tested exclusively in KPPasskeyChecker.Tests (the canonical
    /// source); KP2FAChecker.Tests excludes the whole namespace.
    /// </summary>
    public class TextDetailRowTests
    {
        // --- Two-arg constructor: Value doubles as AccessibleValue ---------------------------------

        [Fact]
        public void TwoArg_ctor_sets_Label_and_Value()
        {
            TextDetailRow row = new TextDetailRow("Passwordless login", "Required");

            Assert.Equal("Passwordless login", row.Label);
            Assert.Equal("Required", row.Value);
        }

        [Fact]
        public void TwoArg_ctor_uses_value_as_AccessibleValue_when_value_is_non_empty()
        {
            TextDetailRow row = new TextDetailRow("Regions", "US, DE");

            Assert.Equal("US, DE", row.AccessibleValue);
        }

        [Fact]
        public void TwoArg_ctor_with_empty_value_falls_back_to_Label_as_AccessibleValue()
        {
            TextDetailRow row = new TextDetailRow("Regions", string.Empty);

            Assert.Equal("Regions", row.AccessibleValue);
        }

        [Fact]
        public void TwoArg_ctor_with_null_value_stores_empty_string_and_falls_back_AccessibleValue_to_Label()
        {
            TextDetailRow row = new TextDetailRow("Regions", null);

            Assert.Equal(string.Empty, row.Value);
            Assert.Equal("Regions", row.AccessibleValue);
        }

        // --- Three-arg constructor: explicit AccessibleValue ----------------------------------------

        [Fact]
        public void ThreeArg_ctor_sets_Label_Value_and_explicit_AccessibleValue_independently()
        {
            TextDetailRow row = new TextDetailRow("MFA", "allowed", "multi-factor authentication: allowed");

            Assert.Equal("MFA", row.Label);
            Assert.Equal("allowed", row.Value);
            Assert.Equal("multi-factor authentication: allowed", row.AccessibleValue);
        }

        [Fact]
        public void ThreeArg_ctor_with_empty_explicit_AccessibleValue_falls_back_to_Label()
        {
            TextDetailRow row = new TextDetailRow("MFA", "allowed", string.Empty);

            Assert.Equal("MFA", row.AccessibleValue);
        }

        [Fact]
        public void ThreeArg_ctor_with_null_explicit_AccessibleValue_falls_back_to_Label()
        {
            TextDetailRow row = new TextDetailRow("MFA", "allowed", null);

            Assert.Equal("MFA", row.AccessibleValue);
        }

        [Fact]
        public void ThreeArg_ctor_with_null_value_stores_empty_string_for_Value()
        {
            TextDetailRow row = new TextDetailRow("MFA", null, "spoken value");

            Assert.Equal(string.Empty, row.Value);
            Assert.Equal("spoken value", row.AccessibleValue);
        }

        // --- Label null/empty handling (inherited from EntryDetailRow) -----------------------------

        [Fact]
        public void Null_label_is_stored_as_empty_string()
        {
            TextDetailRow row = new TextDetailRow(null, "value");

            Assert.Equal(string.Empty, row.Label);
        }

        [Fact]
        public void Empty_label_stays_empty_and_AccessibleValue_still_prefers_a_non_empty_value()
        {
            TextDetailRow row = new TextDetailRow(string.Empty, "value");

            Assert.Equal(string.Empty, row.Label);
            Assert.Equal("value", row.AccessibleValue);
        }

        [Fact]
        public void Empty_label_and_empty_value_yields_empty_AccessibleValue()
        {
            TextDetailRow row = new TextDetailRow(string.Empty, string.Empty);

            Assert.Equal(string.Empty, row.Label);
            Assert.Equal(string.Empty, row.AccessibleValue);
        }
    }
}
