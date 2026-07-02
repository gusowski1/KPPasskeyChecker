using KeePassLib;
using KeePassLib.Security;
using KPPasskeyChecker.Data;
using KPPasskeyChecker.UI;
using Xunit;

namespace KPPasskeyChecker.Tests.UI
{
    /// <summary>
    /// Tests for the entry-list cell formatting (logic changed in v0.5.0 — the "[Active]" bracket
    /// form is now used consistently whether or not the domain is in the directory).
    ///
    /// Full public-(logic-)surface coverage per the P-O DoD addendum (CLAUDE.local.md,
    /// "Definition of Done — Zusatz", 2026-07-02): ComposeCellValue, FormatEntry,
    /// HasStoredPasskey and SupportsCellAction are the logic-bearing members of
    /// PasskeyColumnProvider; all are covered here. ComposeCellValue/FormatEntry/HasStoredPasskey
    /// are ported 1:1 from tools\SelfCheck\SelfCheck.cs (CheckFormatEntry / CheckStoredPasskeyState).
    /// </summary>
    public class PasskeyColumnProviderTests
    {
        [Fact]
        public void SupportsCellAction_column_name_returns_true()
        {
            PasskeyColumnProvider provider = new PasskeyColumnProvider(null);
            Assert.True(provider.SupportsCellAction(PasskeyColumnProvider.ColumnName));
        }

        [Fact]
        public void SupportsCellAction_other_column_name_returns_false()
        {
            PasskeyColumnProvider provider = new PasskeyColumnProvider(null);
            Assert.False(provider.SupportsCellAction("Some Other Column"));
        }

        [Fact]
        public void SupportsCellAction_null_returns_false()
        {
            PasskeyColumnProvider provider = new PasskeyColumnProvider(null);
            Assert.False(provider.SupportsCellAction(null));
        }

        [Theory]
        [InlineData("Login", true, "[Active] Login")]
        [InlineData("Login", false, "[Inactive] Login")]
        [InlineData("", true, "[Active]")]
        [InlineData("", false, "")]
        [InlineData(null, true, "[Active]")]
        [InlineData(null, false, "")]
        public void ComposeCellValue_formats_status_prefix(
            string directoryValue, bool hasStoredPasskey, string expected)
        {
            Assert.Equal(expected, PasskeyColumnProvider.ComposeCellValue(directoryValue, hasStoredPasskey));
        }

        [Fact]
        public void FormatEntry_passwordless_and_mfa_yields_login_plus_2fa()
        {
            PasskeyEntry entry = Entry(PasskeySupportLevel.Allowed, PasskeySupportLevel.Required);
            Assert.Equal("Login + 2FA", PasskeyColumnProvider.FormatEntry(entry));
        }

        [Fact]
        public void FormatEntry_passwordless_only_yields_login()
        {
            PasskeyEntry entry = Entry(PasskeySupportLevel.Allowed, null);
            Assert.Equal("Login", PasskeyColumnProvider.FormatEntry(entry));
        }

        [Fact]
        public void FormatEntry_mfa_only_yields_2fa()
        {
            PasskeyEntry entry = Entry(null, PasskeySupportLevel.Required);
            Assert.Equal("2FA", PasskeyColumnProvider.FormatEntry(entry));
        }

        [Fact]
        public void FormatEntry_neither_yields_empty()
        {
            PasskeyEntry entry = Entry(null, null);
            Assert.Equal(string.Empty, PasskeyColumnProvider.FormatEntry(entry));
        }

        [Fact]
        public void HasStoredPasskey_no_fields_returns_false()
        {
            Assert.False(PasskeyColumnProvider.HasStoredPasskey(EntryWith()));
        }

        [Fact]
        public void HasStoredPasskey_kpex_passkey_field_returns_true()
        {
            Assert.True(PasskeyColumnProvider.HasStoredPasskey(EntryWith("KPEX_PASSKEY_CredentialId")));
        }

        [Fact]
        public void HasStoredPasskey_multiple_kpex_passkey_fields_returns_true()
        {
            Assert.True(PasskeyColumnProvider.HasStoredPasskey(
                EntryWith("KPEX_PASSKEY_CredentialId", "KPEX_PASSKEY_PrivateKey")));
        }

        [Fact]
        public void HasStoredPasskey_lowercase_prefix_returns_true()
        {
            Assert.True(PasskeyColumnProvider.HasStoredPasskey(EntryWith("kpex_passkey_x")));
        }

        [Fact]
        public void HasStoredPasskey_unrelated_standard_fields_return_false()
        {
            Assert.False(PasskeyColumnProvider.HasStoredPasskey(
                EntryWith(PwDefs.UserNameField, PwDefs.PasswordField, PwDefs.TitleField)));
        }

        // Mirrors tools\SelfCheck\SelfCheck.cs Entry(...): builds a PasskeyEntry directly (no
        // directory JSON mapping involved — FormatEntry only reads Passwordless/Mfa).
        private static PasskeyEntry Entry(PasskeySupportLevel? passwordless, PasskeySupportLevel? mfa)
        {
            return new PasskeyEntry { Passwordless = passwordless, Mfa = mfa };
        }

        // Mirrors tools\SelfCheck\SelfCheck.cs EntryWith(...): builds a PwEntry carrying the given
        // string-field names (values are irrelevant — HasStoredPasskey only reads field names).
        private static PwEntry EntryWith(params string[] fieldNames)
        {
            PwEntry pe = new PwEntry(true, true);
            foreach (string name in fieldNames)
                pe.Strings.Set(name, new ProtectedString(false, "x"));
            return pe;
        }
    }
}
