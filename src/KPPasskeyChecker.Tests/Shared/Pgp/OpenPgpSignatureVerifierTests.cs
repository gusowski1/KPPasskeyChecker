using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using KeeRadar.Shared.Pgp;
using Xunit;

namespace KPPasskeyChecker.Tests.Shared.Pgp
{
    /// <summary>
    /// Full public-surface coverage of <see cref="OpenPgpSignatureVerifier"/> — the real-fixture
    /// success path, and every fail-closed branch (wrong key, tampered bytes, malformed input,
    /// decompression-bomb guard), asserting the verifier NEVER throws out of
    /// <see cref="OpenPgpSignatureVerifier.Verify"/> regardless of how hostile the input is).
    /// Ported from <c>tools\SelfCheck\SelfCheck.cs</c> (<c>CheckPgpPath</c>) and
    /// <c>DirectoryTrustAnchorTests</c>'s wrong-key pattern.
    /// Ownership: <c>KeeRadar.Shared.*</c> is tested exclusively in KPPasskeyChecker.Tests (the
    /// canonical source); KP2FAChecker.Tests excludes the whole namespace.
    /// </summary>
    public class OpenPgpSignatureVerifierTests
    {
        // --- constructor -----------------------------------------------------------------------------

        [Fact]
        public void Constructor_throws_on_null_trusted_key()
        {
            Assert.Throws<ArgumentNullException>(() => new OpenPgpSignatureVerifier(null));
        }

        // --- success path against the real fixture ----------------------------------------------------

        [Fact]
        public void Verify_real_fixture_with_the_correct_pinned_key_succeeds()
        {
            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(ReadFixture());

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Verify_real_fixture_returns_the_embedded_literal_content()
        {
            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(ReadFixture());

            Assert.NotNull(result.SignedContent);
            Assert.NotEmpty(result.SignedContent);
        }

        [Fact]
        public void Verify_real_fixture_extracted_content_is_JSON_starting_with_a_brace()
        {
            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(ReadFixture());

            string json = BytesToAscii(result.SignedContent);

            Assert.True(json.Length > 0);
            Assert.Equal('{', json.TrimStart()[0]);
        }

        [Fact]
        public void Verify_real_fixture_returns_the_trusted_keys_key_id_as_issuer()
        {
            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(ReadFixture());

            Assert.Equal(DirectoryTrustAnchor.Key.KeyId, result.IssuerKeyId);
        }

        [Fact]
        public void Verify_real_fixture_error_is_null_on_success()
        {
            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(ReadFixture());

            Assert.Null(result.Error);
        }

        // --- fail-closed: empty / null input -----------------------------------------------------------

        [Fact]
        public void Verify_null_message_returns_invalid_and_does_not_throw()
        {
            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(null);

            Assert.False(result.IsValid);
            Assert.NotNull(result.Error);
        }

        [Fact]
        public void Verify_empty_message_returns_invalid_and_does_not_throw()
        {
            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(new byte[0]);

            Assert.False(result.IsValid);
        }

        // --- fail-closed: wrong trusted key --------------------------------------------------------

        [Fact]
        public void Verify_with_a_wrong_but_well_formed_key_fails_closed_without_throwing()
        {
            PgpVerificationResult result = new OpenPgpSignatureVerifier(CorruptedKey()).Verify(ReadFixture());

            Assert.False(result.IsValid);
            Assert.NotNull(result.Error);
        }

        // --- fail-closed: tampered signed bytes -----------------------------------------------------

        [Fact]
        public void Verify_with_a_single_flipped_byte_in_the_message_fails_closed()
        {
            byte[] tampered = ReadFixture();
            // Flip a byte in the tail (well inside the compressed literal/signature region) so the
            // RSA check (or, if it lands in framing, the packet parse) is guaranteed to reject it.
            tampered[tampered.Length - 5] ^= 0xFF;

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(tampered);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void Verify_with_a_truncated_message_fails_closed_without_throwing()
        {
            byte[] full = ReadFixture();
            byte[] truncated = new byte[full.Length / 2];
            Array.Copy(full, truncated, truncated.Length);

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(truncated);

            Assert.False(result.IsValid);
        }

        // --- fail-closed: malformed packet stream (no compression wrapper) -------------------------

        [Fact]
        public void Verify_a_completely_random_byte_stream_fails_closed_without_throwing()
        {
            byte[] garbage = new byte[64];
            new Random(1234).NextBytes(garbage);
            garbage[0] = 0x00; // ensure the mandatory high tag-bit is unset -> guaranteed parse failure

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(garbage);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void Verify_an_uncompressed_stream_missing_a_literal_data_packet_fails_closed()
        {
            // Old-format tag-2 (Signature) packet only, no literal data, no compression wrapper.
            // 0x80 | (2 << 2) | 0 = 0x88 -> old format, tag 2, 1-byte length.
            byte[] onlySignaturePacket = new byte[] { 0x88, 0x03, 0x04, 0x00, 0x01 };

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(onlySignaturePacket);

            Assert.False(result.IsValid);
            Assert.Contains("literal data", result.Error, StringComparison.Ordinal);
        }

        [Fact]
        public void Verify_unsupported_compression_algorithm_fails_closed()
        {
            // Old-format, indeterminate-length, tag 8 (Compressed Data) wrapping algorithm id 99.
            byte[] message = new byte[] { 0xA3, 99, 0x00, 0x00 };

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(message);

            Assert.False(result.IsValid);
        }

        // --- fail-closed: decompression-bomb guard (16 MB bound) ------------------------------------

        [Fact]
        public void Verify_a_deflate_bomb_beyond_the_16MB_bound_fails_closed_without_throwing()
        {
            byte[] bomb = BuildDeflateBombMessage();

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(bomb);

            Assert.False(result.IsValid);
            Assert.NotNull(result.Error);
        }

        // --- helpers -------------------------------------------------------------------------------

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

        private static string BytesToAscii(byte[] bytes)
        {
            return bytes != null
                ? new string(bytes.Select(b => (char)b).ToArray())
                : string.Empty;
        }

        // Builds an old-format, indeterminate-length, tag-8 (Compressed Data) packet whose body is
        // algorithm=1 (ZIP/raw DEFLATE) followed by a raw-DEFLATE stream of 20 MB of zero bytes
        // (highly compressible -> tiny on the wire, expands past the verifier's 16 MB inflate bound).
        private static byte[] BuildDeflateBombMessage()
        {
            byte[] deflated;
            using (var ms = new MemoryStream())
            {
                using (var deflate = new DeflateStream(ms, CompressionMode.Compress, leaveOpen: true))
                {
                    byte[] zeros = new byte[81920];
                    long remaining = 20L * 1024 * 1024; // 20 MB, above the verifier's 16 MB bound
                    while (remaining > 0)
                    {
                        int chunk = (int)Math.Min(zeros.Length, remaining);
                        deflate.Write(zeros, 0, chunk);
                        remaining -= chunk;
                    }
                }
                deflated = ms.ToArray();
            }

            byte[] body = new byte[1 + deflated.Length];
            body[0] = 1; // algorithm 1 = ZIP (raw DEFLATE)
            Array.Copy(deflated, 0, body, 1, deflated.Length);

            byte[] message = new byte[2 + body.Length];
            message[0] = 0xA3; // old format, tag 8, indeterminate length
            message[1] = body.Length > 255 ? (byte)0 : (byte)body.Length; // unused for length-type 3
            // Length-type 3 is indeterminate: the body simply runs to the end of the buffer, so the
            // second header byte's value is irrelevant to parsing (kept for header-shape clarity).
            Array.Copy(body, 0, message, 2, body.Length);
            return message;
        }
    }
}
