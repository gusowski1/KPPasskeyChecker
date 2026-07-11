using System.Collections.Generic;
using System.Reflection;
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
    /// Full public-(logic-)surface coverage per the project's test-coverage Definition of Done:
    /// ComposeCellValue, FormatEntry, HasStoredPasskey and SupportsCellAction are the logic-bearing
    /// members of
    /// PasskeyColumnProvider; all are covered here. ComposeCellValue/FormatEntry/HasStoredPasskey
    /// are ported 1:1 from tools\SelfCheck\SelfCheck.cs (CheckFormatEntry / CheckStoredPasskeyState).
    ///
    /// Additionally covered here (QA coverage pass, without live KeePass services):
    /// <list type="bullet">
    /// <item><c>GetCellData</c> — the directory-unavailable branch. No test in this assembly ever
    /// calls <see cref="PasskeyDirectoryService.Initialize"/>, so
    /// <see cref="PasskeyDirectoryService.IsAvailable"/> is false for the whole run and
    /// <c>LookupDirectoryValue</c> short-circuits before ever reaching <c>ExtractHost</c> — this
    /// exercises the full public method deterministically, with no live HTTP/service dependency.
    /// The directory-available branch needs a live <see cref="PasskeyDirectoryService"/> (HTTP
    /// fetch + background timer, no injection seam — see its documented
    /// <c>TestCoverageExemptions.Entries</c> reason) and stays out of scope here.</item>
    /// <item><c>PerformCellAction</c> / <c>ShowDetailDialog</c> — only their early-return guard
    /// clauses (wrong column name, null entry). Reaching past the guard calls
    /// <c>EntryDetailForm.ShowDialog()</c> (a modal WinForms dialog), which cannot run headless in
    /// a unit test — deliberately not exercised here.</item>
    /// <item><c>ExtractHost</c> — a private, pure (KeePass-free logic, only reads
    /// <c>PwEntry.Strings</c>) URL-to-host parser, invoked via reflection (same established pattern
    /// as <c>PasskeyDirectoryTests.Build</c> for a non-public static member): missing/blank URL,
    /// scheme-less host, full URL, and a malformed URL that trips the internal catch.</item>
    /// <item><c>Lookup</c> — a private domain-candidate walk over a <see cref="PasskeyDirectory"/>,
    /// invoked via reflection together with the same <c>PasskeyDirectory.Build</c> reflection helper
    /// used by <c>PasskeyDirectoryTests</c>: direct match, subdomain-candidate match, and no match.</item>
    /// </list>
    /// </summary>
    public class PasskeyColumnProviderTests
    {
        [Fact]
        public void ColumnNames_returns_the_single_column_name()
        {
            PasskeyColumnProvider provider = new PasskeyColumnProvider(null);

            Assert.Equal(new[] { PasskeyColumnProvider.ColumnName }, provider.ColumnNames);
        }

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

        // directoryHasData = the directory was consultable for this entry and simply had no match
        // (case (a)). When hasStoredPasskey is true the flag is irrelevant (stored always wins ->
        // "[Active]"). When directoryValue is non-empty the flag is likewise irrelevant (a directory
        // hit always wins). Only the fully-blank + not-stored combination distinguishes "[No Data]"
        // (case a: directory consulted, no hit) from "" (case b/c: not consultable / no URL). null
        // directoryValue is treated the same as empty, consistent with existing behaviour.
        [Theory]
        [InlineData("Login", true, true, "[Active] Login")]
        [InlineData("Login", false, true, "[Inactive] Login")]
        [InlineData("", true, true, "[Active]")]
        [InlineData("", true, false, "[Active]")]
        [InlineData("", false, true, "[No Data]")]
        [InlineData("", false, false, "")]
        [InlineData(null, false, false, "")]
        [InlineData(null, false, true, "[No Data]")]
        public void ComposeCellValue_formats_status_prefix(
            string directoryValue, bool hasStoredPasskey, bool directoryHasData, string expected)
        {
            Assert.Equal(expected,
                PasskeyColumnProvider.ComposeCellValue(directoryValue, hasStoredPasskey, directoryHasData));
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

        // ---- GetCellData: directory-unavailable branch (no live KeePass service needed) ---------

        [Fact]
        public void GetCellData_directory_unavailable_no_stored_passkey_returns_empty()
        {
            Assert.False(
                PasskeyDirectoryService.IsAvailable,
                "Precondition: no test in this assembly calls PasskeyDirectoryService.Initialize, "
                    + "so GetCellData must take the directory-unavailable branch here.");

            PasskeyColumnProvider provider = new PasskeyColumnProvider(null);

            Assert.Equal(string.Empty, provider.GetCellData(PasskeyColumnProvider.ColumnName, EntryWith()));
        }

        [Fact]
        public void GetCellData_directory_unavailable_with_stored_passkey_returns_active()
        {
            Assert.False(
                PasskeyDirectoryService.IsAvailable,
                "Precondition: no test in this assembly calls PasskeyDirectoryService.Initialize, "
                    + "so GetCellData must take the directory-unavailable branch here.");

            PasskeyColumnProvider provider = new PasskeyColumnProvider(null);

            Assert.Equal(
                "[Active]",
                provider.GetCellData(PasskeyColumnProvider.ColumnName, EntryWith("KPEX_PASSKEY_CredentialId")));
        }

        // ---- PerformCellAction / ShowDetailDialog: guard clauses only ---------------------------
        // Reaching past these guards calls EntryDetailForm.ShowDialog() (a modal WinForms dialog),
        // which cannot run headless in a unit test — deliberately not exercised beyond the guard.

        [Fact]
        public void PerformCellAction_wrong_column_name_is_a_no_op()
        {
            PasskeyColumnProvider provider = new PasskeyColumnProvider(null);

            provider.PerformCellAction("Some Other Column", EntryWith());
        }

        [Fact]
        public void PerformCellAction_null_entry_is_a_no_op()
        {
            PasskeyColumnProvider provider = new PasskeyColumnProvider(null);

            provider.PerformCellAction(PasskeyColumnProvider.ColumnName, null);
        }

        [Fact]
        public void ShowDetailDialog_null_entry_is_a_no_op()
        {
            PasskeyColumnProvider provider = new PasskeyColumnProvider(null);

            provider.ShowDetailDialog(null);
        }

        // ---- ExtractHost: private, pure URL-to-host parser (reflection, same pattern as ---------
        // ---- PasskeyDirectoryTests.Build for a non-public static member) ------------------------

        [Fact]
        public void ExtractHost_no_url_field_returns_null()
        {
            Assert.Null(ExtractHost(EntryWith()));
        }

        [Fact]
        public void ExtractHost_blank_url_returns_null()
        {
            PwEntry pe = new PwEntry(true, true);
            pe.Strings.Set(PwDefs.UrlField, new ProtectedString(false, "   "));

            Assert.Null(ExtractHost(pe));
        }

        [Fact]
        public void ExtractHost_scheme_less_host_gets_https_prefixed_and_parsed()
        {
            PwEntry pe = new PwEntry(true, true);
            pe.Strings.Set(PwDefs.UrlField, new ProtectedString(false, "example.com"));

            Assert.Equal("example.com", ExtractHost(pe));
        }

        [Fact]
        public void ExtractHost_full_url_with_scheme_returns_host()
        {
            PwEntry pe = new PwEntry(true, true);
            pe.Strings.Set(PwDefs.UrlField, new ProtectedString(false, "https://example.com/path?q=1"));

            Assert.Equal("example.com", ExtractHost(pe));
        }

        [Fact]
        public void ExtractHost_malformed_url_is_caught_and_returns_null()
        {
            // Already contains "://" (so no https:// prefix is applied) but has no host component
            // -> new Uri(...) throws UriFormatException, caught internally, returns null.
            PwEntry pe = new PwEntry(true, true);
            pe.Strings.Set(PwDefs.UrlField, new ProtectedString(false, "https://"));

            Assert.Null(ExtractHost(pe));
        }

        // ---- Lookup: private domain-candidate walk over a PasskeyDirectory (reflection) ---------

        [Fact]
        public void Lookup_direct_host_match_returns_entry()
        {
            PasskeyDirectory dir = BuildDirectory(RawWithDomain("example.com"));

            PasskeyEntry entry = Lookup(dir, "example.com");

            Assert.NotNull(entry);
            Assert.Equal("example.com", entry.PrimaryDomain);
        }

        [Fact]
        public void Lookup_subdomain_walks_up_to_registrable_domain_match()
        {
            PasskeyDirectory dir = BuildDirectory(RawWithDomain("example.com"));

            PasskeyEntry entry = Lookup(dir, "mail.example.com");

            Assert.NotNull(entry);
            Assert.Equal("example.com", entry.PrimaryDomain);
        }

        [Fact]
        public void Lookup_no_match_anywhere_returns_null()
        {
            PasskeyDirectory dir = BuildDirectory(RawWithDomain("example.com"));

            Assert.Null(Lookup(dir, "totally-different.example"));
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

        private static string ExtractHost(PwEntry pe)
        {
            MethodInfo method = typeof(PasskeyColumnProvider).GetMethod(
                "ExtractHost", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method); // fails loudly if the internal signature ever changes
            return (string)method.Invoke(null, new object[] { pe });
        }

        private static PasskeyEntry Lookup(PasskeyDirectory dir, string host)
        {
            MethodInfo method = typeof(PasskeyColumnProvider).GetMethod(
                "Lookup", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method); // fails loudly if the internal signature ever changes
            return (PasskeyEntry)method.Invoke(null, new object[] { dir, host });
        }

        // Mirrors PasskeyDirectoryTests.Build: PasskeyDirectory.Build is internal, invoked via
        // reflection (same signature/pattern already pinned there).
        private static PasskeyDirectory BuildDirectory(Dictionary<string, object> raw)
        {
            MethodInfo method = typeof(PasskeyDirectory).GetMethod(
                "Build", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method); // fails loudly if the internal signature ever changes
            return (PasskeyDirectory)method.Invoke(null, new object[] { raw });
        }

        private static Dictionary<string, object> RawWithDomain(string domain)
        {
            return new Dictionary<string, object>
            {
                { domain, new Dictionary<string, object> { { "mfa", "allowed" } } }
            };
        }
    }
}
