using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KeeRadar.Shared.Pgp;

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

            CheckDirectoryTrustAnchorFingerprint();
            CheckPgpPath();
            CheckSharedTreeNotDiverged();

            Console.WriteLine();
            if (_failures == 0)
            {
                Console.WriteLine("All checks passed.");
                return 0;
            }

            Console.WriteLine(_failures + " check(s) FAILED.");
            return 1;
        }

        // --- DirectoryTrustAnchor fingerprint assertion (shared) --------------------------------
        // Delegates to SharedChecks which references the canonical KeeRadar.Shared.Pgp.DirectoryTrustAnchor.
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
        private static OpenPgpRsaPublicKey CorruptedKey()
        {
            byte[] rdata = SharedChecks.HexToBytes(DirectoryTrustAnchor.CertRecordHex);
            rdata[rdata.Length - 8] ^= 0xFF; // flip a byte well inside the modulus
            return OpenPgpRsaPublicKey.FromCertRecord(rdata);
        }

        // --- Shared-tree drift guard -------------------------------------------------------------
        // Release-blocking gate: compares the REAL two repo trees on disk. KPPasskeyChecker\src\Shared
        // is canonical; KP2FAChecker\src\Shared must be byte-identical (kept in sync via
        // sync-shared.ps1). FAILs (non-empty divergence list) when the sibling repo's copy has
        // drifted. The pure comparison/detection algorithm itself (SharedTreeComparer) is pinned by
        // a synthetic-fixture xUnit regression test (Architecture\SharedTreeDriftGuardTests.cs); this
        // check exercises it against the actual on-disk trees, which is why it stays in the SDK-free
        // SelfCheck harness rather than in the xUnit project (it must stay SDK-free).
        private static void CheckSharedTreeNotDiverged()
        {
            Section("Shared-tree drift guard (KPPasskeyChecker vs KP2FAChecker)");

            string here = AppDomain.CurrentDomain.BaseDirectory;
            // The harness .exe is staged directly under tools\ (see run-selfcheck.ps1's $OutExe),
            // so the repo root is one level up.
            string repoRoot = Path.GetFullPath(Path.Combine(here, @".."));
            string canonicalShared = Path.Combine(repoRoot, @"src\Shared");
            string siblingShared = Path.GetFullPath(Path.Combine(repoRoot, @"..\KP2FAChecker\src\Shared"));

            if (!Directory.Exists(siblingShared))
            {
                Assert(
                    "sibling KP2FAChecker\\src\\Shared not found at " + siblingShared
                        + " (skip — not a Lockstep checkout)",
                    true);
                return;
            }

            IReadOnlyList<string> divergent = SharedTreeComparer.FindDivergentFiles(canonicalShared, siblingShared);
            Assert(
                "KPPasskeyChecker\\src\\Shared and KP2FAChecker\\src\\Shared are byte-identical"
                    + (divergent.Count == 0 ? string.Empty
                        : " (divergent: " + string.Join(", ", divergent.ToArray()) + ")"),
                divergent.Count == 0);
        }

        // --- helpers ---------------------------------------------------------------------------
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
