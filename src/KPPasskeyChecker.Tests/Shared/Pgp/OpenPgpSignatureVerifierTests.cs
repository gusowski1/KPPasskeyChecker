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

        // --- fail-closed: malformed signature-packet fields (hand-built, no one-pass packet) --------

        [Fact]
        public void Verify_signature_packet_shorter_than_six_bytes_fails_closed()
        {
            byte[] signatureBody = new byte[] { 4, 0, 1, 10, 0 }; // one byte short of the minimum header
            byte[] message = Concat(LiteralDataPacket(SamplePayload()), OldFormatPacket(PacketTagSignature, signatureBody));

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(message);

            Assert.False(result.IsValid);
            Assert.Contains("too short", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Verify_signature_with_an_unsupported_version_fails_closed()
        {
            byte[] signatureBody = new byte[] { 3, 0, 1, 10, 0, 0 };
            byte[] message = Concat(LiteralDataPacket(SamplePayload()), OldFormatPacket(PacketTagSignature, signatureBody));

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(message);

            Assert.False(result.IsValid);
            Assert.Contains("Unsupported signature version", result.Error, StringComparison.Ordinal);
        }

        [Fact]
        public void Verify_signature_with_an_unexpected_signature_type_fails_closed()
        {
            byte[] signatureBody = new byte[] { 4, 5, 1, 10, 0, 0 };
            byte[] message = Concat(LiteralDataPacket(SamplePayload()), OldFormatPacket(PacketTagSignature, signatureBody));

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(message);

            Assert.False(result.IsValid);
            Assert.Contains("Unexpected signature type", result.Error, StringComparison.Ordinal);
        }

        [Fact]
        public void Verify_signature_with_an_unsupported_public_key_algorithm_fails_closed()
        {
            byte[] signatureBody = new byte[] { 4, 0, 99, 10, 0, 0 };
            byte[] message = Concat(LiteralDataPacket(SamplePayload()), OldFormatPacket(PacketTagSignature, signatureBody));

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(message);

            Assert.False(result.IsValid);
            Assert.Contains("Unsupported public-key algorithm", result.Error, StringComparison.Ordinal);
        }

        [Fact]
        public void Verify_signature_with_an_unsupported_hash_algorithm_fails_closed()
        {
            byte[] signatureBody = new byte[] { 4, 0, 1, 7, 0, 0 };
            byte[] message = Concat(LiteralDataPacket(SamplePayload()), OldFormatPacket(PacketTagSignature, signatureBody));

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(message);

            Assert.False(result.IsValid);
            Assert.Contains("Unsupported hash algorithm", result.Error, StringComparison.Ordinal);
        }

        [Fact]
        public void Verify_signature_with_a_hashed_subpacket_length_exceeding_the_packet_fails_closed()
        {
            // Declares 100 bytes of hashed subpackets that are never actually appended.
            byte[] signatureBody = new byte[] { 4, 0, 1, 10, 0, 100 };
            byte[] message = Concat(LiteralDataPacket(SamplePayload()), OldFormatPacket(PacketTagSignature, signatureBody));

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(message);

            Assert.False(result.IsValid);
            Assert.Contains("Truncated hashed subpackets", result.Error, StringComparison.Ordinal);
        }

        // --- issuer key id resolution when no one-pass-signature packet is present ------------------
        // These cover OpenPgpSignatureVerifier.FindIssuerKeyId / ScanSubpacketsForIssuer, which are
        // only reached when the signed message carries no one-pass-signature packet (the real .sig
        // fixture always does, so the success-path tests above never exercise this fallback).

        [Fact]
        public void Verify_signature_without_a_one_pass_packet_resolves_the_issuer_from_hashed_subpackets_and_fails_the_rsa_check()
        {
            byte[] hashedSubpackets = IssuerKeyIdSubpacket(DirectoryTrustAnchor.Key.KeyId);
            byte[] signatureBody = SignaturePacketBodyReachingRsaCheck(10, hashedSubpackets, new byte[0]);
            byte[] message = Concat(LiteralDataPacket(SamplePayload()), OldFormatPacket(PacketTagSignature, signatureBody));

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(message);

            Assert.False(result.IsValid);
            Assert.Contains("RSA signature does not match", result.Error, StringComparison.Ordinal);
        }

        [Fact]
        public void Verify_signature_without_a_one_pass_packet_resolves_the_issuer_from_unhashed_subpackets_and_rejects_a_mismatch()
        {
            byte[] unhashedSubpackets = IssuerKeyIdSubpacket("0000000000000000"); // deliberately wrong key id
            byte[] signatureBody;
            using (var stream = new MemoryStream())
            {
                stream.WriteByte(4);
                stream.WriteByte(0x00);
                stream.WriteByte(1);
                stream.WriteByte(10);
                WriteUInt16BE(stream, 0); // no hashed subpackets
                WriteUInt16BE(stream, unhashedSubpackets.Length);
                stream.Write(unhashedSubpackets, 0, unhashedSubpackets.Length);
                // No trailer: VerifySignature returns on the issuer mismatch before it is ever read.
                signatureBody = stream.ToArray();
            }
            byte[] message = Concat(LiteralDataPacket(SamplePayload()), OldFormatPacket(PacketTagSignature, signatureBody));

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(message);

            Assert.False(result.IsValid);
            Assert.Contains("does not match trusted key", result.Error, StringComparison.Ordinal);
        }

        [Fact]
        public void Verify_signature_with_no_resolvable_issuer_anywhere_still_reaches_the_rsa_check()
        {
            // One unrecognized-type (250) hashed subpacket; the scan walks over it and finds nothing,
            // in both the hashed and (empty) unhashed regions, so the issuer check short-circuits.
            byte[] hashedSubpackets = new byte[] { 4, 250, 0xAA, 0xBB, 0xCC };
            byte[] signatureBody = SignaturePacketBodyReachingRsaCheck(10, hashedSubpackets, new byte[0]);
            byte[] message = Concat(LiteralDataPacket(SamplePayload()), OldFormatPacket(PacketTagSignature, signatureBody));

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(message);

            Assert.False(result.IsValid);
            Assert.Contains("RSA signature does not match", result.Error, StringComparison.Ordinal);
        }

        [Fact]
        public void Verify_signature_with_a_two_byte_length_encoded_subpacket_is_skipped_before_finding_the_issuer()
        {
            // RFC 4880 4.2.2.2: a subpacket length in [192, 255) is encoded across two bytes. This one
            // (first=192, second=0) declares a 192-byte body of an unrecognized type, which the scan
            // must skip correctly before it reaches the real issuer subpacket that follows it.
            byte[] paddingSubpacket = new byte[194];
            paddingSubpacket[0] = 192;
            paddingSubpacket[1] = 0;
            paddingSubpacket[2] = 250; // unrecognized subpacket type
            byte[] hashedSubpackets = Concat(paddingSubpacket, IssuerKeyIdSubpacket(DirectoryTrustAnchor.Key.KeyId));
            byte[] signatureBody = SignaturePacketBodyReachingRsaCheck(10, hashedSubpackets, new byte[0]);
            byte[] message = Concat(LiteralDataPacket(SamplePayload()), OldFormatPacket(PacketTagSignature, signatureBody));

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(message);

            Assert.False(result.IsValid);
            Assert.Contains("RSA signature does not match", result.Error, StringComparison.Ordinal);
        }

        // --- fail-closed: truncated signature MPI framing --------------------------------------------
        // Each variant truncates the byte stream right before one of ReadSignatureMpi's three explicit
        // length checks; none of these need a one-pass-signature packet because the (matching) hashed/
        // unhashed subpacket regions are empty, so issuer resolution safely short-circuits first.

        [Fact]
        public void Verify_signature_missing_the_unhashed_subpacket_length_field_fails_closed()
        {
            byte[] signatureBody = new byte[] { 4, 0, 1, 10, 0, 0 }; // stops right after the (empty) hashed subpackets
            byte[] message = Concat(LiteralDataPacket(SamplePayload()), OldFormatPacket(PacketTagSignature, signatureBody));

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(message);

            Assert.False(result.IsValid);
            Assert.Contains("Truncated signature MPI", result.Error, StringComparison.Ordinal);
        }

        [Fact]
        public void Verify_signature_missing_the_hash_prefix_bytes_after_unhashed_subpackets_fails_closed()
        {
            // hashedLen=0, unhashedLen=4, four filler bytes, then nothing else.
            byte[] signatureBody = new byte[] { 4, 0, 1, 10, 0, 0, 0, 4, 0, 0, 0, 0 };
            byte[] message = Concat(LiteralDataPacket(SamplePayload()), OldFormatPacket(PacketTagSignature, signatureBody));

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(message);

            Assert.False(result.IsValid);
            Assert.Contains("Truncated signature MPI", result.Error, StringComparison.Ordinal);
        }

        [Fact]
        public void Verify_signature_missing_the_mpi_bit_length_field_fails_closed()
        {
            // hashedLen=0, unhashedLen=0, two hash-prefix bytes, then nothing else.
            byte[] signatureBody = new byte[] { 4, 0, 1, 10, 0, 0, 0, 0, 0, 0 };
            byte[] message = Concat(LiteralDataPacket(SamplePayload()), OldFormatPacket(PacketTagSignature, signatureBody));

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(message);

            Assert.False(result.IsValid);
            Assert.Contains("Truncated signature MPI", result.Error, StringComparison.Ordinal);
        }

        // --- hash algorithm support ------------------------------------------------------------------
        // The real fixture only ever exercises SHA-512 (hash algorithm id 10); these reach TryMapHash's
        // other two supported cases and CreateHash's matching branches.

        [Fact]
        public void Verify_signature_with_the_SHA256_hash_algorithm_reaches_the_rsa_check()
        {
            byte[] signatureBody = SignaturePacketBodyReachingRsaCheck(8, new byte[0], new byte[0]);
            byte[] message = Concat(LiteralDataPacket(SamplePayload()), OldFormatPacket(PacketTagSignature, signatureBody));

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(message);

            Assert.False(result.IsValid);
            Assert.Contains("RSA signature does not match", result.Error, StringComparison.Ordinal);
        }

        [Fact]
        public void Verify_signature_with_the_SHA384_hash_algorithm_reaches_the_rsa_check()
        {
            byte[] signatureBody = SignaturePacketBodyReachingRsaCheck(9, new byte[0], new byte[0]);
            byte[] message = Concat(LiteralDataPacket(SamplePayload()), OldFormatPacket(PacketTagSignature, signatureBody));

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(message);

            Assert.False(result.IsValid);
            Assert.Contains("RSA signature does not match", result.Error, StringComparison.Ordinal);
        }

        // --- zlib-wrapped compression (algorithm 2) ---------------------------------------------------
        // The real fixture always uses algorithm 1 (raw DEFLATE); this exercises the sibling
        // algorithm-2 branch, which skips a 2-byte zlib header before the same raw-DEFLATE decoder.

        [Fact]
        public void Verify_a_zlib_wrapped_compressed_message_is_decompressed_without_throwing()
        {
            byte[] inner = LiteralDataPacket(SamplePayload());
            byte[] deflated;
            using (var compressed = new MemoryStream())
            {
                using (var deflate = new DeflateStream(compressed, CompressionMode.Compress, true))
                    deflate.Write(inner, 0, inner.Length);
                deflated = compressed.ToArray();
            }
            byte[] body = Concat(new byte[] { 2, 0x78, 0x9C }, deflated); // algorithm 2 (zlib); the header bytes are skipped
            byte[] message = OldFormatPacket(PacketTagCompressedData, body);

            PgpVerificationResult result = DirectoryTrustAnchor.CreateVerifier().Verify(message);

            Assert.False(result.IsValid);
            Assert.Contains("signature packet", result.Error, StringComparison.OrdinalIgnoreCase);
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

        // --- packet-construction helpers for the hand-built messages above -------------------------
        // Mirrors the OpenPGP packet tags/framing rules OpenPgpSignatureVerifier and PgpPacketReader
        // implement (RFC 4880 4.2/5.2/5.9), so the constructed byte arrays exercise the real parser.

        private const int PacketTagSignature = 2;
        private const int PacketTagCompressedData = 8;
        private const int PacketTagLiteralData = 11;

        private static byte[] SamplePayload()
        {
            return new byte[] { 0x01, 0x02, 0x03, 0x04 };
        }

        private static byte[] Concat(params byte[][] parts)
        {
            int total = parts.Sum(p => p.Length);
            byte[] result = new byte[total];
            int offset = 0;
            foreach (byte[] part in parts)
            {
                Array.Copy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }
            return result;
        }

        private static void WriteUInt16BE(MemoryStream stream, int value)
        {
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)(value & 0xFF));
        }

        // Old-format packet header (RFC 4880 4.2.1), definite length (1- or 2-byte form — plenty for
        // the small hand-built messages here).
        private static byte[] OldFormatPacket(int tag, byte[] body)
        {
            if (body.Length <= 255)
                return Concat(new byte[] { (byte)(0x80 | (tag << 2) | 0x00), (byte)body.Length }, body);

            return Concat(
                new byte[]
                {
                    (byte)(0x80 | (tag << 2) | 0x01),
                    (byte)((body.Length >> 8) & 0xFF),
                    (byte)(body.Length & 0xFF)
                },
                body);
        }

        // Literal Data packet body: format(1) fileNameLen(1) fileName(n) date(4) content(...) — no
        // file name, zero modification date, since OpenPgpSignatureVerifier never inspects either.
        private static byte[] LiteralDataPacket(byte[] content)
        {
            byte[] body = new byte[6 + content.Length];
            body[0] = (byte)'b'; // binary data format
            body[1] = 0;         // no file name
            Array.Copy(content, 0, body, 6, content.Length);
            return OldFormatPacket(PacketTagLiteralData, body);
        }

        // A single Issuer Key ID subpacket (RFC 4880 5.2.3.5, type 16): one-byte length (9 = type +
        // 8-byte key id) followed by the type byte and the 8-byte key id itself.
        private static byte[] IssuerKeyIdSubpacket(string keyIdHex)
        {
            byte[] keyId = HexToBytes(keyIdHex);
            byte[] subpacket = new byte[1 + 1 + 8];
            subpacket[0] = 9;
            subpacket[1] = 16;
            Array.Copy(keyId, 0, subpacket, 2, 8);
            return subpacket;
        }

        // Builds a v4 signature packet body with a full, well-formed trailer (hash-prefix bytes, MPI
        // bit length, an MPI value sized to the trusted key's modulus) so verification runs all the way
        // to the RSA check. The MPI value is deliberately not a real signature (a tiny numeric value,
        // guaranteed smaller than the modulus so RSA.VerifyHash returns false instead of throwing), so
        // these messages always land on OpenPgpSignatureVerifier's "RSA signature does not match"
        // branch without needing the real private key.
        private static byte[] SignaturePacketBodyReachingRsaCheck(byte hashAlgo, byte[] hashedSubpackets, byte[] unhashedSubpackets)
        {
            int modulusLength = DirectoryTrustAnchor.Key.Modulus.Length;
            byte[] mpiValue = new byte[modulusLength];
            mpiValue[modulusLength - 1] = 0x07;

            using (var stream = new MemoryStream())
            {
                stream.WriteByte(4);    // version
                stream.WriteByte(0x00); // signature type: binary document
                stream.WriteByte(1);    // public-key algorithm: RSA
                stream.WriteByte(hashAlgo);
                WriteUInt16BE(stream, hashedSubpackets.Length);
                stream.Write(hashedSubpackets, 0, hashedSubpackets.Length);
                WriteUInt16BE(stream, unhashedSubpackets.Length);
                stream.Write(unhashedSubpackets, 0, unhashedSubpackets.Length);
                stream.WriteByte(0);
                stream.WriteByte(0); // left 16 bits of the hash (not checked by the verifier)
                WriteUInt16BE(stream, modulusLength * 8);
                stream.Write(mpiValue, 0, mpiValue.Length);
                return stream.ToArray();
            }
        }
    }
}
