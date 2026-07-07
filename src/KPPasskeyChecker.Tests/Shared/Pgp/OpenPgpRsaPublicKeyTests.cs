using System;
using KeeRadar.Shared.Pgp;
using Xunit;

namespace KPPasskeyChecker.Tests.Shared.Pgp
{
    /// <summary>
    /// Full public-surface coverage of <see cref="OpenPgpRsaPublicKey"/> —
    /// <see cref="OpenPgpRsaPublicKey.FromCertRecord"/> parsing, fingerprint / key-id computation,
    /// and fail-closed behaviour on malformed/truncated input. The real, pinned CERT RDATA
    /// (<see cref="DirectoryTrustAnchor.CertRecordHex"/>) is reused as the valid fixture so the
    /// parsed fingerprint can be cross-checked against the known-good expected value without
    /// embedding a second key.
    /// Ownership: <c>KeeRadar.Shared.*</c> is tested exclusively in KPPasskeyChecker.Tests (the
    /// canonical source); KP2FAChecker.Tests excludes the whole namespace.
    /// </summary>
    public class OpenPgpRsaPublicKeyTests
    {
        // --- FromCertRecord: happy path ----------------------------------------------------------

        [Fact]
        public void FromCertRecord_parses_the_real_pinned_CERT_record()
        {
            OpenPgpRsaPublicKey key = OpenPgpRsaPublicKey.FromCertRecord(RealRdata());

            Assert.NotNull(key);
        }

        [Fact]
        public void FromCertRecord_computes_the_expected_pinned_fingerprint()
        {
            OpenPgpRsaPublicKey key = OpenPgpRsaPublicKey.FromCertRecord(RealRdata());

            Assert.Equal(DirectoryTrustAnchor.ExpectedFingerprint, key.Fingerprint, ignoreCase: true);
        }

        [Fact]
        public void FromCertRecord_fingerprint_is_40_uppercase_hex_chars()
        {
            OpenPgpRsaPublicKey key = OpenPgpRsaPublicKey.FromCertRecord(RealRdata());

            Assert.Equal(40, key.Fingerprint.Length);
            Assert.Equal(key.Fingerprint.ToUpperInvariant(), key.Fingerprint);
        }

        [Fact]
        public void FromCertRecord_key_id_is_the_low_16_hex_chars_of_the_fingerprint()
        {
            OpenPgpRsaPublicKey key = OpenPgpRsaPublicKey.FromCertRecord(RealRdata());

            Assert.Equal(16, key.KeyId.Length);
            Assert.Equal(key.Fingerprint.Substring(key.Fingerprint.Length - 16), key.KeyId);
        }

        [Fact]
        public void FromCertRecord_populates_non_empty_modulus_and_exponent()
        {
            OpenPgpRsaPublicKey key = OpenPgpRsaPublicKey.FromCertRecord(RealRdata());

            Assert.NotNull(key.Modulus);
            Assert.NotEmpty(key.Modulus);
            Assert.NotNull(key.Exponent);
            Assert.NotEmpty(key.Exponent);
        }

        [Fact]
        public void FromCertRecord_RSA_4096_modulus_is_512_bytes()
        {
            // The pinned key is documented as RSA-4096 (CLAUDE.md); 4096 bits = 512 bytes.
            OpenPgpRsaPublicKey key = OpenPgpRsaPublicKey.FromCertRecord(RealRdata());

            Assert.Equal(512, key.Modulus.Length);
        }

        // --- FromCertRecord: fail-closed on malformed / truncated RDATA --------------------------

        [Fact]
        public void FromCertRecord_throws_on_null_rdata()
        {
            Assert.Throws<ArgumentException>(() => OpenPgpRsaPublicKey.FromCertRecord(null));
        }

        [Fact]
        public void FromCertRecord_throws_on_rdata_shorter_than_6_bytes()
        {
            Assert.Throws<ArgumentException>(() => OpenPgpRsaPublicKey.FromCertRecord(new byte[5]));
        }

        [Fact]
        public void FromCertRecord_throws_when_cert_type_is_not_PGP_3()
        {
            byte[] rdata = RealRdata();
            rdata[0] = 0x00;
            rdata[1] = 0x01; // cert type 1 (X.509), not 3 (PGP)

            Assert.Throws<ArgumentException>(() => OpenPgpRsaPublicKey.FromCertRecord(rdata));
        }

        [Fact]
        public void FromCertRecord_throws_when_the_embedded_packet_is_truncated()
        {
            byte[] full = RealRdata();
            byte[] truncated = new byte[10];
            Array.Copy(full, truncated, truncated.Length);

            Assert.ThrowsAny<Exception>(() => OpenPgpRsaPublicKey.FromCertRecord(truncated));
        }

        // --- FromPublicKeyPacket: fail-closed ------------------------------------------------------

        [Fact]
        public void FromPublicKeyPacket_throws_on_null_packet()
        {
            Assert.Throws<ArgumentException>(() => OpenPgpRsaPublicKey.FromPublicKeyPacket(null));
        }

        [Fact]
        public void FromPublicKeyPacket_throws_on_packet_shorter_than_3_bytes()
        {
            Assert.Throws<ArgumentException>(() => OpenPgpRsaPublicKey.FromPublicKeyPacket(new byte[2]));
        }

        [Fact]
        public void FromPublicKeyPacket_throws_when_the_tag_is_not_6()
        {
            // Old-format header, tag 2 (Signature packet: (0x80 | (2 << 2) | 0) = 0x88), length 1, one body byte.
            byte[] packet = new byte[] { 0x88, 0x01, 0x00 };

            Assert.Throws<ArgumentException>(() => OpenPgpRsaPublicKey.FromPublicKeyPacket(packet));
        }

        [Fact]
        public void FromPublicKeyPacket_throws_when_declared_body_length_exceeds_the_buffer()
        {
            // Old-format header, tag 6, 1-byte length field declaring 250 bytes but buffer far shorter.
            byte[] packet = new byte[] { (byte)(0x80 | (6 << 2) | 0), 250, 0x04 };

            Assert.Throws<ArgumentException>(() => OpenPgpRsaPublicKey.FromPublicKeyPacket(packet));
        }

        [Fact]
        public void FromPublicKeyPacket_throws_on_unsupported_key_version()
        {
            byte[] body = new byte[] { 3, 0, 0, 0, 0, 1 }; // version 3 (unsupported), then padding + algo RSA
            byte[] packet = WrapAsTag6Packet(body);

            Assert.Throws<NotSupportedException>(() => OpenPgpRsaPublicKey.FromPublicKeyPacket(packet));
        }

        [Fact]
        public void FromPublicKeyPacket_throws_on_unsupported_algorithm()
        {
            byte[] body = new byte[] { 4, 0, 0, 0, 0, 99 }; // version 4, algo 99 (unsupported)
            byte[] packet = WrapAsTag6Packet(body);

            Assert.Throws<NotSupportedException>(() => OpenPgpRsaPublicKey.FromPublicKeyPacket(packet));
        }

        [Fact]
        public void FromPublicKeyPacket_throws_on_truncated_MPI_length()
        {
            // version 4, algo RSA(1), then only 1 byte of the 2-byte MPI bit-length field.
            byte[] body = new byte[] { 4, 0, 0, 0, 0, 1, 0x00 };
            byte[] packet = WrapAsTag6Packet(body);

            Assert.Throws<ArgumentException>(() => OpenPgpRsaPublicKey.FromPublicKeyPacket(packet));
        }

        [Fact]
        public void FromPublicKeyPacket_throws_on_truncated_MPI_value()
        {
            // version 4, algo RSA(1), MPI declares 16 bits (2 bytes) but body ends immediately after the length.
            byte[] body = new byte[] { 4, 0, 0, 0, 0, 1, 0x00, 0x10 };
            byte[] packet = WrapAsTag6Packet(body);

            Assert.Throws<ArgumentException>(() => OpenPgpRsaPublicKey.FromPublicKeyPacket(packet));
        }

        [Fact]
        public void FromPublicKeyPacket_accepts_RSA_encrypt_only_algorithm_id_2()
        {
            byte[] modulus = new byte[] { 0xFF, 0xFF }; // 16 bits
            byte[] exponent = new byte[] { 0x01 }; // 8 bits (leading bit clear -> exact bit count irrelevant here)
            byte[] body = BuildV4RsaBody(2, modulus, exponent);
            byte[] packet = WrapAsTag6Packet(body);

            OpenPgpRsaPublicKey key = OpenPgpRsaPublicKey.FromPublicKeyPacket(packet);

            Assert.NotNull(key);
            Assert.Equal(modulus, key.Modulus);
        }

        [Fact]
        public void FromPublicKeyPacket_accepts_RSA_sign_only_algorithm_id_3()
        {
            byte[] modulus = new byte[] { 0x7F };
            byte[] exponent = new byte[] { 0x03 };
            byte[] body = BuildV4RsaBody(3, modulus, exponent);
            byte[] packet = WrapAsTag6Packet(body);

            OpenPgpRsaPublicKey key = OpenPgpRsaPublicKey.FromPublicKeyPacket(packet);

            Assert.NotNull(key);
            Assert.Equal(exponent, key.Exponent);
        }

        [Fact]
        public void FromPublicKeyPacket_reads_a_minimal_valid_RSA_key_correctly()
        {
            byte[] modulus = new byte[] { 0x81, 0x02, 0x03 }; // 3 bytes, top bit set -> 17 bits declared
            byte[] exponent = new byte[] { 0x01, 0x00, 0x01 }; // 65537, needs 17 bits -> 3 bytes

            byte[] body = BuildV4RsaBody(1, modulus, exponent);
            byte[] packet = WrapAsTag6Packet(body);

            OpenPgpRsaPublicKey key = OpenPgpRsaPublicKey.FromPublicKeyPacket(packet);

            Assert.Equal(modulus, key.Modulus);
            Assert.Equal(exponent, key.Exponent);
            Assert.Equal(40, key.Fingerprint.Length);
        }

        // --- helpers -------------------------------------------------------------------------------

        private static byte[] RealRdata()
        {
            return HexToBytes(DirectoryTrustAnchor.CertRecordHex);
        }

        private static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        // Wraps an arbitrary public-key packet body in an old-format tag-6 header with a 2-byte length.
        private static byte[] WrapAsTag6Packet(byte[] body)
        {
            byte[] packet = new byte[3 + body.Length];
            packet[0] = (byte)(0x80 | (6 << 2) | 1); // old format, tag 6, 2-byte length
            packet[1] = (byte)((body.Length >> 8) & 0xFF);
            packet[2] = (byte)(body.Length & 0xFF);
            Array.Copy(body, 0, packet, 3, body.Length);
            return packet;
        }

        // Builds a version-4 RSA public-key packet body: version(1) + creation-time(4) + algo(1) +
        // modulus-MPI + exponent-MPI, with MPI bit-lengths computed from the supplied byte arrays
        // (top bit of the first byte assumed significant, matching the fixtures used above).
        private static byte[] BuildV4RsaBody(byte algo, byte[] modulus, byte[] exponent)
        {
            int modBits = (modulus.Length - 1) * 8 + BitLengthOfTopByte(modulus[0]);
            int expBits = (exponent.Length - 1) * 8 + BitLengthOfTopByte(exponent[0]);

            byte[] body = new byte[1 + 4 + 1 + 2 + modulus.Length + 2 + exponent.Length];
            int p = 0;
            body[p++] = 4; // version
            p += 4; // creation time (unused by the parser)
            body[p++] = algo;
            body[p++] = (byte)((modBits >> 8) & 0xFF);
            body[p++] = (byte)(modBits & 0xFF);
            Array.Copy(modulus, 0, body, p, modulus.Length);
            p += modulus.Length;
            body[p++] = (byte)((expBits >> 8) & 0xFF);
            body[p++] = (byte)(expBits & 0xFF);
            Array.Copy(exponent, 0, body, p, exponent.Length);
            return body;
        }

        private static int BitLengthOfTopByte(byte b)
        {
            int bits = 8;
            for (int mask = 0x80; mask > 0; mask >>= 1, bits--)
                if ((b & mask) != 0) return bits;
            return 0;
        }
    }
}
