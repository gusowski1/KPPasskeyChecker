using System;
using KeeRadar.Shared.Pgp;
using Xunit;

namespace KPPasskeyChecker.Tests.Shared.Pgp
{
    /// <summary>
    /// Full public-surface coverage of <see cref="PgpPacketReader"/> — old- and new-format headers,
    /// every length encoding, and fail-closed (throwing) behaviour on malformed/truncated input,
    /// since this internal reader is only ever consumed from inside try/catch call sites that turn
    /// exceptions into an invalid result.
    /// <see cref="PgpPacketReader"/> is <c>internal</c>; visible here via
    /// <c>[InternalsVisibleTo("KPPasskeyChecker.Tests")]</c> on the plugin assembly (which compiles
    /// <c>src\Shared</c> in as source).
    /// Ownership: <c>KeeRadar.Shared.*</c> is tested exclusively in KPPasskeyChecker.Tests (the
    /// canonical source); KP2FAChecker.Tests excludes the whole namespace.
    /// </summary>
    public class PgpPacketReaderTests
    {
        // --- new format, 1-byte length (< 192) -----------------------------------------------------

        [Fact]
        public void ReadHeader_new_format_1_byte_length_reports_correct_tag_and_body()
        {
            // 0xC0 | tag -> new format, tag 11 (Literal Data): 0xC0 | 0x0B = 0xCB.
            byte[] buf = new byte[] { 0xCB, 0x05, 1, 2, 3, 4, 5 };
            int pos = 0;
            int bodyStart, bodyLen;

            int tag = PgpPacketReader.ReadHeader(buf, ref pos, out bodyStart, out bodyLen);

            Assert.Equal(11, tag);
            Assert.Equal(2, bodyStart);
            Assert.Equal(5, bodyLen);
            Assert.Equal(2, pos);
        }

        // --- new format, 2-byte length (192..223 prefix byte) ---------------------------------------

        [Fact]
        public void ReadHeader_new_format_2_byte_length_computes_length_per_RFC4880()
        {
            // New format, tag 2 (Signature): 0xC0 | 0x02 = 0xC2.
            // l0=192 (0xC0), l1=0x00 -> bodyLen = ((192-192)<<8) + 0 + 192 = 192.
            byte[] buf = new byte[2 + 192];
            buf[0] = 0xC2;
            buf[1] = 192;
            buf[2] = 0;
            int pos = 0;
            int bodyStart, bodyLen;

            int tag = PgpPacketReader.ReadHeader(buf, ref pos, out bodyStart, out bodyLen);

            Assert.Equal(2, tag);
            Assert.Equal(3, bodyStart);
            Assert.Equal(192, bodyLen);
        }

        // --- new format, 5-byte length (prefix byte 255) ---------------------------------------------

        [Fact]
        public void ReadHeader_new_format_5_byte_length_computes_length_per_RFC4880()
        {
            byte[] buf = new byte[6 + 300];
            buf[0] = 0xCB; // new format, tag 11
            buf[1] = 255;
            buf[2] = 0x00;
            buf[3] = 0x00;
            buf[4] = 0x01;
            buf[5] = 0x2C; // 0x0000012C = 300
            int pos = 0;
            int bodyStart, bodyLen;

            int tag = PgpPacketReader.ReadHeader(buf, ref pos, out bodyStart, out bodyLen);

            Assert.Equal(11, tag);
            Assert.Equal(6, bodyStart);
            Assert.Equal(300, bodyLen);
        }

        [Fact]
        public void ReadHeader_new_format_partial_body_length_is_not_supported()
        {
            // Prefix byte in [224..254] signals a partial body length (power-of-two chunk).
            byte[] buf = new byte[] { 0xCB, 224, 0, 0 };
            int pos = 0;
            int bodyStart, bodyLen;

            Assert.Throws<NotSupportedException>(() =>
                PgpPacketReader.ReadHeader(buf, ref pos, out bodyStart, out bodyLen));
        }

        // --- old format, all four length-type encodings ---------------------------------------------

        [Fact]
        public void ReadHeader_old_format_1_byte_length()
        {
            // Old format, tag 8 (Compressed Data), length-type 0 (1-byte length): 0x80 | (8<<2) | 0 = 0xA0.
            byte[] buf = new byte[] { 0xA0, 0x03, 9, 9, 9 };
            int pos = 0;
            int bodyStart, bodyLen;

            int tag = PgpPacketReader.ReadHeader(buf, ref pos, out bodyStart, out bodyLen);

            Assert.Equal(8, tag);
            Assert.Equal(2, bodyStart);
            Assert.Equal(3, bodyLen);
        }

        [Fact]
        public void ReadHeader_old_format_2_byte_length()
        {
            // Old format, tag 8, length-type 1 (2-byte length): 0x80 | (8<<2) | 1 = 0xA1.
            byte[] buf = new byte[] { 0xA1, 0x01, 0x00 }.CopyAndPad(0x100);
            int pos = 0;
            int bodyStart, bodyLen;

            int tag = PgpPacketReader.ReadHeader(buf, ref pos, out bodyStart, out bodyLen);

            Assert.Equal(8, tag);
            Assert.Equal(3, bodyStart);
            Assert.Equal(0x100, bodyLen);
        }

        [Fact]
        public void ReadHeader_old_format_4_byte_length()
        {
            // Old format, tag 8, length-type 2 (4-byte length): 0x80 | (8<<2) | 2 = 0xA2.
            byte[] header = new byte[] { 0xA2, 0x00, 0x00, 0x00, 0x02 };
            byte[] buf = new byte[header.Length + 2];
            Array.Copy(header, buf, header.Length);
            int pos = 0;
            int bodyStart, bodyLen;

            int tag = PgpPacketReader.ReadHeader(buf, ref pos, out bodyStart, out bodyLen);

            Assert.Equal(8, tag);
            Assert.Equal(5, bodyStart);
            Assert.Equal(2, bodyLen);
        }

        [Fact]
        public void ReadHeader_old_format_indeterminate_length_runs_to_end_of_buffer()
        {
            // Old format, tag 8, length-type 3 (indeterminate): 0x80 | (8<<2) | 3 = 0xA3.
            // Real .sig files start with A3 01 per CLAUDE.md.
            byte[] buf = new byte[] { 0xA3, 0x01, 0xAA, 0xBB, 0xCC };
            int pos = 0;
            int bodyStart, bodyLen;

            int tag = PgpPacketReader.ReadHeader(buf, ref pos, out bodyStart, out bodyLen);

            Assert.Equal(8, tag);
            Assert.Equal(1, bodyStart);
            Assert.Equal(4, bodyLen); // buf.Length(5) - bodyStart(1)
        }

        // --- fail-closed: malformed / truncated input -------------------------------------------------

        [Fact]
        public void ReadHeader_throws_when_pos_is_at_end_of_buffer()
        {
            byte[] buf = new byte[] { 0xA0, 0x00 };
            int pos = 2;
            int bodyStart, bodyLen;

            Assert.Throws<ArgumentException>(() =>
                PgpPacketReader.ReadHeader(buf, ref pos, out bodyStart, out bodyLen));
        }

        [Fact]
        public void ReadHeader_throws_when_the_high_bit_of_the_tag_byte_is_not_set()
        {
            byte[] buf = new byte[] { 0x00, 0x01, 0x02 };
            int pos = 0;
            int bodyStart, bodyLen;

            Assert.Throws<ArgumentException>(() =>
                PgpPacketReader.ReadHeader(buf, ref pos, out bodyStart, out bodyLen));
        }

        [Fact]
        public void ReadHeader_throws_on_truncated_new_format_length_byte()
        {
            byte[] buf = new byte[] { 0xCB }; // new format tag, no length byte follows
            int pos = 0;
            int bodyStart, bodyLen;

            Assert.Throws<ArgumentException>(() =>
                PgpPacketReader.ReadHeader(buf, ref pos, out bodyStart, out bodyLen));
        }

        [Fact]
        public void ReadHeader_throws_on_truncated_new_format_2_byte_length()
        {
            byte[] buf = new byte[] { 0xCB, 200 }; // prefix in [192,223) but no second length byte
            int pos = 0;
            int bodyStart, bodyLen;

            Assert.Throws<ArgumentException>(() =>
                PgpPacketReader.ReadHeader(buf, ref pos, out bodyStart, out bodyLen));
        }

        [Fact]
        public void ReadHeader_throws_on_truncated_new_format_5_byte_length()
        {
            byte[] buf = new byte[] { 0xCB, 255, 0x00, 0x00 }; // prefix 255 but only 2 of 4 length bytes
            int pos = 0;
            int bodyStart, bodyLen;

            Assert.Throws<ArgumentException>(() =>
                PgpPacketReader.ReadHeader(buf, ref pos, out bodyStart, out bodyLen));
        }

        [Fact]
        public void ReadHeader_throws_on_truncated_old_format_2_byte_length()
        {
            byte[] buf = new byte[] { 0xA1, 0x00 }; // length-type 1 needs 2 length bytes, only 1 present
            int pos = 0;
            int bodyStart, bodyLen;

            Assert.Throws<ArgumentException>(() =>
                PgpPacketReader.ReadHeader(buf, ref pos, out bodyStart, out bodyLen));
        }

        [Fact]
        public void ReadHeader_throws_on_truncated_old_format_4_byte_length()
        {
            byte[] buf = new byte[] { 0xA2, 0x00, 0x00, 0x00 }; // length-type 2 needs 4 length bytes, only 3 present
            int pos = 0;
            int bodyStart, bodyLen;

            Assert.Throws<ArgumentException>(() =>
                PgpPacketReader.ReadHeader(buf, ref pos, out bodyStart, out bodyLen));
        }

        // --- pos advancement across consecutive packets -----------------------------------------------

        [Fact]
        public void ReadHeader_advances_pos_so_a_second_packet_can_be_read_immediately_after()
        {
            byte[] first = new byte[] { 0xA0, 0x02, 0x11, 0x22 };
            byte[] second = new byte[] { 0xA0, 0x01, 0x33 };
            byte[] buf = new byte[first.Length + second.Length];
            Array.Copy(first, buf, first.Length);
            Array.Copy(second, 0, buf, first.Length, second.Length);

            int pos = 0;
            int bodyStart1, bodyLen1;
            int tag1 = PgpPacketReader.ReadHeader(buf, ref pos, out bodyStart1, out bodyLen1);
            pos = bodyStart1 + bodyLen1;

            int bodyStart2, bodyLen2;
            int tag2 = PgpPacketReader.ReadHeader(buf, ref pos, out bodyStart2, out bodyLen2);

            Assert.Equal(8, tag1);
            Assert.Equal(8, tag2);
            Assert.Equal(first.Length + 2, bodyStart2); // second packet's body starts after its own 2-byte header
            Assert.Equal(1, bodyLen2);
        }
    }

    internal static class ByteArrayPadExtensions
    {
        // Pads a short prefix with trailing zero bytes so old-format 2-byte-length fixtures can
        // declare a body length larger than the literal bytes written inline in the test.
        public static byte[] CopyAndPad(this byte[] prefix, int totalBodyLength)
        {
            byte[] result = new byte[3 + totalBodyLength];
            Array.Copy(prefix, result, prefix.Length);
            return result;
        }
    }
}
