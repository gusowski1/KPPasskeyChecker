using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KeePassLib;
using KeePassLib.Security;
using KPPasskeyChecker.Data;
using KeeRadar.Shared.Pgp;
using KPPasskeyChecker.UI;
// TDD (ROT): DirectoryTrustAnchor does not yet exist; this using will cause a compile error
// until the coder creates KPPasskeyChecker/src/Shared/Pgp/DirectoryTrustAnchor.cs.

namespace KPPasskeyChecker.SelfCheck
{
    /// <summary>
    /// Pure-logic self-test harness for KPPasskeyChecker. Compiled together with the real plugin
    /// sources (Shared + KPPasskeyChecker) by run-selfcheck.ps1 using the in-box csc (C# 5), so it
    /// can reach internal types like PasskeyEntryMapper. It exercises only logic that has no
    /// dependency on a running KeePass process, the network, or the file system.
    ///
    /// Exit code 0 = all checks passed; exit code 1 = at least one check failed (stops at the
    /// first failure). Every assertion prints a single PASS/FAIL line.
    /// </summary>
    internal static class SelfCheck
    {
        private static int _failures;

        private static int Main()
        {
            Console.WriteLine("KPPasskeyChecker self-check");
            Console.WriteLine("===========================");

            CheckSupportLevelParsing();
            CheckScopeEndpointMapping();
            CheckSignedCacheKeyDistinctness();
            CheckFormatEntry();
            CheckStoredPasskeyState();
            CheckDomainCandidatesEtldPlusOne();
            CheckDirectoryTrustAnchorFingerprint();
            CheckPgpPath();

            Console.WriteLine();
            if (_failures == 0)
            {
                Console.WriteLine("All checks passed.");
                return 0;
            }

            Console.WriteLine(_failures + " check(s) FAILED.");
            return 1;
        }

        // --- mfa / passwordless parsing (PasskeyEntryMapper.ParseLevel via Map) ----------------
        private static void CheckSupportLevelParsing()
        {
            Section("mfa / passwordless parsing");

            PasskeyEntry allowed = Map("example.com", Field("passwordless", "allowed"));
            Assert("passwordless \"allowed\" -> Allowed",
                allowed.Passwordless == PasskeySupportLevel.Allowed);

            PasskeyEntry required = Map("example.com", Field("mfa", "required"));
            Assert("mfa \"required\" -> Required",
                required.Mfa == PasskeySupportLevel.Required);

            PasskeyEntry missing = Map("example.com", new Dictionary<string, object>());
            Assert("missing passwordless -> null",
                missing.Passwordless == null);
            Assert("missing mfa -> null",
                missing.Mfa == null);

            PasskeyEntry invalid = Map("example.com", Field("mfa", "sometimes"));
            Assert("invalid mfa value -> null (fail-safe)",
                invalid.Mfa == null);

            PasskeyEntry mixedCase = Map("example.com", Field("passwordless", "ReQuIrEd"));
            Assert("case-insensitive \"ReQuIrEd\" -> Required",
                mixedCase.Passwordless == PasskeySupportLevel.Required);
        }

        // --- scope -> endpoint mapping (PasskeyEndpoints.ForScope) ------------------------------
        private static void CheckScopeEndpointMapping()
        {
            Section("scope -> endpoint mapping");

            Assert("PasswordlessOnly -> passwordless.json",
                PasskeyEndpoints.ForScope(PasskeyDataScope.PasswordlessOnly)
                    .EndsWith("/passwordless.json", StringComparison.Ordinal));
            Assert("MfaOnly -> mfa.json",
                PasskeyEndpoints.ForScope(PasskeyDataScope.MfaOnly)
                    .EndsWith("/mfa.json", StringComparison.Ordinal));
            Assert("AnySupport -> supported.json",
                PasskeyEndpoints.ForScope(PasskeyDataScope.AnySupport)
                    .EndsWith("/supported.json", StringComparison.Ordinal));

            // The signature endpoint is the JSON endpoint plus ".sig".
            Assert("SignatureForScope appends .sig",
                PasskeyEndpoints.SignatureForScope(PasskeyDataScope.MfaOnly)
                    == PasskeyEndpoints.ForScope(PasskeyDataScope.MfaOnly) + ".sig");
        }

        // --- signed vs unsigned cache-key distinctness -----------------------------------------
        private static void CheckSignedCacheKeyDistinctness()
        {
            Section("signed / unsigned cache-key distinctness");

            foreach (PasskeyDataScope scope in
                     (PasskeyDataScope[])Enum.GetValues(typeof(PasskeyDataScope)))
            {
                string plain  = PasskeyEndpoints.CacheKey(scope);
                string signed = PasskeyEndpoints.SignedCacheKey(scope);
                Assert("CacheKey != SignedCacheKey for " + scope, plain != signed);
                Assert("SignedCacheKey for " + scope + " carries _signed suffix",
                    signed.EndsWith("_signed", StringComparison.Ordinal));
                Assert("plain CacheKey for " + scope + " has no _signed suffix",
                    !plain.EndsWith("_signed", StringComparison.Ordinal));
            }
        }

        // --- FormatEntry / column value --------------------------------------------------------
        // Exercises the production PasskeyColumnProvider.FormatEntry directly (it is internal and
        // reachable here because the harness is compiled together with the plugin sources).
        private static void CheckFormatEntry()
        {
            Section("FormatEntry / column value");

            Assert("passwordless + mfa -> \"Login + 2FA\"",
                PasskeyColumnProvider.FormatEntry(Entry(PasskeySupportLevel.Allowed, PasskeySupportLevel.Required)) == "Login + 2FA");
            Assert("passwordless only -> \"Login\"",
                PasskeyColumnProvider.FormatEntry(Entry(PasskeySupportLevel.Allowed, null)) == "Login");
            Assert("mfa only -> \"2FA\"",
                PasskeyColumnProvider.FormatEntry(Entry(null, PasskeySupportLevel.Required)) == "2FA");
            Assert("neither -> empty",
                PasskeyColumnProvider.FormatEntry(Entry(null, null)) == string.Empty);
        }

        // --- stored-passkey state (story P-J) --------------------------------------------------
        // Exercises the production HasStoredPasskey field-name scan and the ComposeCellValue overlay
        // covering all seven story scenarios. HasStoredPasskey runs against a real PwEntry (the
        // KeePass assembly is already JIT-loaded by the FormatEntry checks above).
        private static void CheckStoredPasskeyState()
        {
            Section("stored-passkey state (column overlay)");

            // ComposeCellValue truth table -----------------------------------------------------
            // 2: directory match + stored passkey -> "[Active] <value>" (one prefix only).
            Assert("dir \"Login\" + stored -> \"[Active] Login\"",
                PasskeyColumnProvider.ComposeCellValue("Login", true) == "[Active] Login");
            // 1: directory match + no stored passkey -> "[Inactive] <value>".
            Assert("dir \"Login\" + not stored -> \"[Inactive] Login\"",
                PasskeyColumnProvider.ComposeCellValue("Login", false) == "[Inactive] Login");
            // 3 + 5: no directory match + stored passkey -> "[Active]" (consistent bracket form).
            Assert("no dir + stored -> \"[Active]\"",
                PasskeyColumnProvider.ComposeCellValue(string.Empty, true) == "[Active]");
            // 4 + 5: neither -> empty.
            Assert("no dir + not stored -> empty",
                PasskeyColumnProvider.ComposeCellValue(string.Empty, false) == string.Empty);
            Assert("null dir + stored -> \"[Active]\"",
                PasskeyColumnProvider.ComposeCellValue(null, true) == "[Active]");

            // HasStoredPasskey field-name scan -------------------------------------------------
            Assert("no fields -> not stored",
                !PasskeyColumnProvider.HasStoredPasskey(EntryWith()));
            Assert("KPEX_PASSKEY_ field -> stored",
                PasskeyColumnProvider.HasStoredPasskey(EntryWith("KPEX_PASSKEY_CredentialId")));
            // 6: multiple matching fields still count as one (and ComposeCellValue never doubles).
            Assert("multiple KPEX_PASSKEY_ fields -> stored (no double prefix)",
                PasskeyColumnProvider.HasStoredPasskey(
                    EntryWith("KPEX_PASSKEY_CredentialId", "KPEX_PASSKEY_PrivateKey")));
            // Case-insensitive prefix match.
            Assert("lowercase kpex_passkey_ field -> stored",
                PasskeyColumnProvider.HasStoredPasskey(EntryWith("kpex_passkey_x")));
            // Unrelated standard/custom fields must not trigger.
            Assert("UserName/Password/Title -> not stored",
                !PasskeyColumnProvider.HasStoredPasskey(
                    EntryWith(PwDefs.UserNameField, PwDefs.PasswordField, PwDefs.TitleField)));
        }

        // Builds a PwEntry carrying the given string-field names (empty values; the production code
        // only reads names). The .sig/value is irrelevant — scenario 7 forbids reading values.
        private static PwEntry EntryWith(params string[] fieldNames)
        {
            PwEntry pe = new PwEntry(true, true);
            foreach (string name in fieldNames)
                pe.Strings.Set(name, new ProtectedString(false, "x"));
            return pe;
        }

        // --- PSL / eTLD+1 smoke test (shared with KP2FAChecker via SharedChecks.cs) --------------
        private static void CheckDomainCandidatesEtldPlusOne()
        {
            SharedChecks.CheckDomainCandidatesEtldPlusOne(Section, Assert);
        }

        // --- DirectoryTrustAnchor fingerprint assertion (shared) --------------------------------
        // TDD (ROT): delegates to SharedChecks which references the canonical
        // KeeRadar.Shared.Pgp.DirectoryTrustAnchor. This causes a compile error until the coder
        // creates KPPasskeyChecker/src/Shared/Pgp/DirectoryTrustAnchor.cs.
        private static void CheckDirectoryTrustAnchorFingerprint()
        {
            SharedChecks.CheckDirectoryTrustAnchorFingerprint(Section, Assert);
        }

        // --- PGP path --------------------------------------------------------------------------
        // Exercises the full offline verification path against a committed real ".sig" fixture
        // (an RSA-4096 / SHA-512 inline OpenPGP message captured from passkeys-api.2fa.directory)
        // using the canonical pinned DirectoryTrustAnchor key (not the plugin-local PasskeyTrustAnchor).
        // No network access: the fixture is read from disk next to the harness .exe.
        // A second pass uses a deliberately corrupted key to prove that a wrong key fails closed.
        //
        // TDD (ROT): DirectoryTrustAnchor.CreateVerifier() does not yet exist; compile error expected.
        private static void CheckPgpPath()
        {
            Section("PGP signature path");

            string fixturePath = Path.Combine(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures"), "mfa.json.sig");
            byte[] sigBytes = File.ReadAllBytes(fixturePath);

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(sigBytes);
            Assert("Verify(fixture) returns valid result with non-null JSON",
                result.IsValid && result.SignedContent != null);

            string json = result.SignedContent != null
                ? new string(result.SignedContent.Select(b => (char)b).ToArray()) : string.Empty;
            Assert("Extracted JSON is valid (non-empty, starts with '{')",
                json.Length > 0 && json.TrimStart()[0] == '{');

            PgpVerificationResult wrongKey =
                new OpenPgpSignatureVerifier(CorruptedKey()).Verify(sigBytes);
            Assert("Verify with wrong key returns invalid result", !wrongKey.IsValid);
        }

        // Builds an RSA public key from the pinned CERT RDATA with a single modulus byte flipped,
        // so it parses cleanly but can never match the real signature (fail-closed wrong-key case).
        // TDD (ROT): DirectoryTrustAnchor.CertRecordHex does not yet exist; compile error expected.
        private static OpenPgpRsaPublicKey CorruptedKey()
        {
            byte[] rdata = SharedChecks.HexToBytes(DirectoryTrustAnchor.CertRecordHex);
            rdata[rdata.Length - 8] ^= 0xFF; // flip a byte well inside the modulus
            return OpenPgpRsaPublicKey.FromCertRecord(rdata);
        }

        // --- helpers ---------------------------------------------------------------------------
        private static PasskeyEntry Map(string domain, Dictionary<string, object> data)
        {
            return PasskeyEntryMapper.Map(domain, data);
        }

        private static Dictionary<string, object> Field(string key, object value)
        {
            return new Dictionary<string, object> { { key, value } };
        }

        private static PasskeyEntry Entry(PasskeySupportLevel? passwordless, PasskeySupportLevel? mfa)
        {
            return new PasskeyEntry { Passwordless = passwordless, Mfa = mfa };
        }

        private static void Section(string title)
        {
            Console.WriteLine();
            Console.WriteLine("[" + title + "]");
        }

        private static void Assert(string description, bool condition)
        {
            if (condition)
            {
                Console.WriteLine("  PASS  " + description);
                return;
            }
            _failures++;
            Console.WriteLine("  FAIL  " + description);
        }
    }
}
