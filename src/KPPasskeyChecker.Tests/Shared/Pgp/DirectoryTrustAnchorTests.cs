using System;
using System.IO;
using System.Linq;
using KeeRadar.Shared.Pgp;
using Xunit;

namespace KPPasskeyChecker.Tests.Shared.Pgp
{
    /// <summary>
    /// Tests for the pinned 2fa.directory code-signing key (new in v0.5.0 — replaces the former
    /// per-plugin *TrustAnchor). The fingerprint pin is the fail-closed security invariant.
    ///
    /// The verify-path tests below reference "fixtures\mfa.json.sig" relative to the test-output
    /// directory (AppContext.BaseDirectory), ported 1:1 from tools\SelfCheck\SelfCheck.cs
    /// (CheckPgpPath). The fixture file is wired via the csproj CopyToOutputDirectory setting.
    /// </summary>
    public class DirectoryTrustAnchorTests
    {
        [Fact]
        public void Key_loads_and_matches_the_pinned_fingerprint()
        {
            OpenPgpRsaPublicKey key = DirectoryTrustAnchor.Key;

            Assert.NotNull(key);
            Assert.Equal(DirectoryTrustAnchor.ExpectedFingerprint, key.Fingerprint, ignoreCase: true);
        }

        [Fact]
        public void CreateVerifier_returns_a_verifier()
        {
            Assert.NotNull(DirectoryTrustAnchor.CreateVerifier());
        }

        [Fact]
        public void Verify_real_fixture_returns_valid_result_with_json_content()
        {
            byte[] sigBytes = ReadFixture();

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(sigBytes);

            Assert.True(result.IsValid);
            Assert.NotNull(result.SignedContent);
        }

        [Fact]
        public void Verify_real_fixture_extracted_json_starts_with_brace()
        {
            byte[] sigBytes = ReadFixture();

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(sigBytes);

            string json = result.SignedContent != null
                ? new string(result.SignedContent.Select(b => (char)b).ToArray())
                : string.Empty;

            Assert.True(json.Length > 0);
            Assert.Equal('{', json.TrimStart()[0]);
        }

        [Fact]
        public void Verify_with_wrong_key_fails_closed()
        {
            byte[] sigBytes = ReadFixture();

            PgpVerificationResult wrongKeyResult =
                new OpenPgpSignatureVerifier(CorruptedKey()).Verify(sigBytes);

            Assert.False(wrongKeyResult.IsValid);
        }

        // Mirrors tools\SelfCheck\SelfCheck.cs fixture lookup: read next to the test binary
        // ("fixtures\mfa.json.sig" under the test-output directory), not next to the source file.
        private static byte[] ReadFixture()
        {
            string fixturePath = Path.Combine(
                Path.Combine(AppContext.BaseDirectory, "fixtures"), "mfa.json.sig");
            return File.ReadAllBytes(fixturePath);
        }

        // Mirrors tools\SelfCheck\SelfCheck.cs CorruptedKey(): builds an RSA public key from the
        // pinned CERT RDATA with a single modulus byte flipped, so it parses cleanly but can never
        // match the real signature (fail-closed wrong-key case).
        private static OpenPgpRsaPublicKey CorruptedKey()
        {
            byte[] rdata = HexToBytes(DirectoryTrustAnchor.CertRecordHex);
            rdata[rdata.Length - 8] ^= 0xFF; // flip a byte well inside the modulus
            return OpenPgpRsaPublicKey.FromCertRecord(rdata);
        }

        private static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
    }
}
