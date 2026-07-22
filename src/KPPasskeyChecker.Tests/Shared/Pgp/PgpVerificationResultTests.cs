using KeeRadar.Shared.Pgp;
using Xunit;

namespace KPPasskeyChecker.Tests.Shared.Pgp
{
    /// <summary>
    /// Full public-surface coverage of <see cref="PgpVerificationResult"/> — both factory methods
    /// and every property's state in each outcome, including the "not set on the other branch"
    /// default states.
    /// Ownership: <c>KeeRadar.Shared.*</c> is tested exclusively in KPPasskeyChecker.Tests (the
    /// canonical source); KP2FAChecker.Tests excludes the whole namespace.
    /// </summary>
    public class PgpVerificationResultTests
    {
        // --- Valid(...) ----------------------------------------------------------------------------

        [Fact]
        public void Valid_sets_IsValid_true()
        {
            PgpVerificationResult result = PgpVerificationResult.Valid(new byte[] { 1, 2, 3 }, "ABCD1234ABCD1234");

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Valid_stores_the_signed_content_reference()
        {
            byte[] content = new byte[] { 0x7B, 0x7D }; // "{}"

            PgpVerificationResult result = PgpVerificationResult.Valid(content, "ABCD1234ABCD1234");

            Assert.Same(content, result.SignedContent);
        }

        [Fact]
        public void Valid_stores_the_issuer_key_id()
        {
            PgpVerificationResult result = PgpVerificationResult.Valid(new byte[0], "AD8483C1CBABC36D");

            Assert.Equal("AD8483C1CBABC36D", result.IssuerKeyId);
        }

        [Fact]
        public void Valid_leaves_Error_null()
        {
            PgpVerificationResult result = PgpVerificationResult.Valid(new byte[0], "AD8483C1CBABC36D");

            Assert.Null(result.Error);
        }

        [Fact]
        public void Valid_accepts_an_empty_content_array()
        {
            PgpVerificationResult result = PgpVerificationResult.Valid(new byte[0], "AD8483C1CBABC36D");

            Assert.NotNull(result.SignedContent);
            Assert.Empty(result.SignedContent);
        }

        [Fact]
        public void Valid_accepts_a_null_issuer_key_id()
        {
            PgpVerificationResult result = PgpVerificationResult.Valid(new byte[] { 1 }, null);

            Assert.Null(result.IssuerKeyId);
            Assert.True(result.IsValid);
        }

        // --- Invalid(...) ----------------------------------------------------------------------------

        [Fact]
        public void Invalid_sets_IsValid_false()
        {
            PgpVerificationResult result = PgpVerificationResult.Invalid("Empty signature file.");

            Assert.False(result.IsValid);
        }

        [Fact]
        public void Invalid_stores_the_error_message()
        {
            PgpVerificationResult result = PgpVerificationResult.Invalid("Malformed signed message: boom");

            Assert.Equal("Malformed signed message: boom", result.Error);
        }

        [Fact]
        public void Invalid_leaves_SignedContent_null()
        {
            PgpVerificationResult result = PgpVerificationResult.Invalid("No literal data packet found.");

            Assert.Null(result.SignedContent);
        }

        [Fact]
        public void Invalid_leaves_IssuerKeyId_null()
        {
            PgpVerificationResult result = PgpVerificationResult.Invalid("No signature packet found.");

            Assert.Null(result.IssuerKeyId);
        }

        [Fact]
        public void Invalid_accepts_a_null_error_message()
        {
            PgpVerificationResult result = PgpVerificationResult.Invalid(null);

            Assert.False(result.IsValid);
            Assert.Null(result.Error);
        }

        // --- independence between instances ---------------------------------------------------------

        [Fact]
        public void Valid_and_Invalid_instances_do_not_share_state()
        {
            PgpVerificationResult valid = PgpVerificationResult.Valid(new byte[] { 9 }, "KEYID000KEYID000");
            PgpVerificationResult invalid = PgpVerificationResult.Invalid("some error");

            Assert.True(valid.IsValid);
            Assert.False(invalid.IsValid);
            Assert.Null(valid.Error);
            Assert.Null(invalid.SignedContent);
            Assert.Null(invalid.IssuerKeyId);
        }
    }
}
