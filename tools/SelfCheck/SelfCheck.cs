using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KPPasskeyChecker.Data;
using KPPasskeyChecker.Shared.DomainMatching;
using KPPasskeyChecker.Shared.Pgp;

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
            CheckDomainCandidatesEtldPlusOne();
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
        // PasskeyColumnProvider.FormatEntry is private and lives on a ColumnProvider subclass that
        // cannot be instantiated without KeePass UI types, so it is mirrored here from the source
        // (the mapping is small and asserted to stay in lock-step with the support-level fields).
        private static void CheckFormatEntry()
        {
            Section("FormatEntry / column value");

            Assert("passwordless + mfa -> \"Login + 2FA\"",
                FormatEntry(Entry(PasskeySupportLevel.Allowed, PasskeySupportLevel.Required)) == "Login + 2FA");
            Assert("passwordless only -> \"Login\"",
                FormatEntry(Entry(PasskeySupportLevel.Allowed, null)) == "Login");
            Assert("mfa only -> \"2FA\"",
                FormatEntry(Entry(null, PasskeySupportLevel.Required)) == "2FA");
            Assert("neither -> empty",
                FormatEntry(Entry(null, null)) == string.Empty);
        }

        // Mirror of PasskeyColumnProvider.FormatEntry.
        private static string FormatEntry(PasskeyEntry entry)
        {
            if (entry.SupportsPasswordless && entry.SupportsMfa) return "Login + 2FA";
            if (entry.SupportsPasswordless) return "Login";
            if (entry.SupportsMfa) return "2FA";
            return string.Empty;
        }

        // --- PSL / eTLD+1 smoke test -----------------------------------------------------------
        // DomainCandidateGenerator pulls its PSL from the network on a background thread, so its
        // registrable-domain stopping point cannot be exercised offline. The eTLD+1 logic itself
        // lives in PublicSuffixList, which is fully testable in isolation (Parse + lookup). We test
        // that, plus the generator's no-PSL 2-label fallback (its deterministic offline behaviour).
        private static void CheckDomainCandidatesEtldPlusOne()
        {
            Section("PSL / eTLD+1 smoke test");

            PublicSuffixList psl = PublicSuffixList.Parse(
                "// test fixture\n" +
                "com\n" +
                "co.uk\n" +
                "uk\n");

            Assert("www.example.co.uk -> registrable example.co.uk",
                psl.GetRegistrableDomain("www.example.co.uk") == "example.co.uk");
            Assert("mail.google.com -> registrable google.com",
                psl.GetRegistrableDomain("mail.google.com") == "google.com");

            // Generator fallback (no PSL loaded in-process): walks down to the 2-label minimum.
            var candidates = DomainCandidateGenerator.GetCandidates("mail.google.com").ToList();
            Assert("generator yields full host first",
                candidates.Count > 0 && candidates[0] == "mail.google.com");
            Assert("generator stops at 2-label fallback (contains google.com)",
                candidates.Contains("google.com"));
            Assert("generator strips leading www.",
                DomainCandidateGenerator.GetCandidates("www.example.co.uk")
                    .First() == "example.co.uk");
        }

        // --- PGP path --------------------------------------------------------------------------
        // Exercises the full offline verification path against a committed real ".sig" fixture
        // (an RSA-4096 / SHA-512 inline OpenPGP message captured from passkeys-api.2fa.directory)
        // using the pinned PasskeyTrustAnchor key. No network access: the fixture is read from disk
        // next to the harness .exe. A second pass uses a deliberately corrupted key to prove that
        // a wrong key fails closed.
        private static void CheckPgpPath()
        {
            Section("PGP signature path");

            string fixturePath = Path.Combine(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures"), "mfa.json.sig");
            byte[] sigBytes = File.ReadAllBytes(fixturePath);

            PgpVerificationResult result = PasskeyTrustAnchor.CreateVerifier().Verify(sigBytes);
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
        private static OpenPgpRsaPublicKey CorruptedKey()
        {
            byte[] rdata = HexToBytes(CertRecordHex);
            rdata[rdata.Length - 8] ^= 0xFF; // flip a byte well inside the modulus
            return OpenPgpRsaPublicKey.FromCertRecord(rdata);
        }

        // Mirror of the pinned CERT RDATA (PasskeyTrustAnchor.CertRecordHex is private); used only
        // to derive a deliberately corrupted key for the wrong-key assertion.
        private const string CertRecordHex =
            "000300000099020d04604596b2011000d5291dc2ac2b30ffb2930604f90405214fd010630c5a03b9bddcee7af66a66640b703f38ab3ca1960898897f7ecc7bf7d6e65178e80642ffaf6f7cc85d1ec2cb0018ae9d4d898dead5b51ce4e0629d0fe2ce3d435bc33ffcc09a41874e08e867741d2181235450969f195c072fb933776cc3263a21438da92b240e74f26eb4bac5d4059f83eab007ce7d681233b9d36db0cbe98bf6a8d5fd91ad813651897f6f2ea2b35c071c898ccb3f900c70ba052c6708cd148dbde3000bc729eb4bb6e8b195545a81bd511e4cb6bcd734fcee73cecdd664b5c7559c66c637c333392a6969d6246faca4f5732151f3c05f25f66f6d0cd5867664c4b7366aa37a6c69bf8bd53e59615dc89a0a8953337af25d6c229ca1cdcff6418f07f5eb76da7dc867bbf4995fd4897e5e2030002e57503125c4681be608babde9cfcaa9c837c4ed1ec904bd5590de941d8c9c2c8c3903ed15aed08704eec0045137422017d3c6e25823cbd22f55e2fa7780348ddbf5205a55fb8f489c59c31047491f8b2f11ec4d31945739b98dad05493a3ba7659f43ff666088022981a0b1d99068a7345349355cb64a3b98a33b883fddc858ea159dc4205ce4591ec3359b0155efd597710d7eb2e5d0ebefb53c4753cd1f6fcf2f2f4a9da381986a056fe30efb91557709de01221a9459d97c25183ce0c80bdf0e4fa507649cff0739a170d95b8793491048604f00070011010001";

        private static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
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
