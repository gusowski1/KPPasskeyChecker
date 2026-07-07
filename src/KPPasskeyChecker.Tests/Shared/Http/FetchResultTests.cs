using KeeRadar.Shared.Http;
using Xunit;

namespace KPPasskeyChecker.Tests.Shared.Http
{
    /// <summary>
    /// Full public-surface coverage of <see cref="FetchResult"/> — all four factory methods, both
    /// <c>Success</c> overloads, plain property round-trip, and the fields each outcome leaves at
    /// their default. Ownership: <c>KeeRadar.Shared.*</c> is tested exclusively in
    /// KPPasskeyChecker.Tests (the canonical source); KP2FAChecker.Tests excludes the whole
    /// namespace.
    /// </summary>
    public class FetchResultTests
    {
        // --- Success(string, string) --------------------------------------------------------------

        [Fact]
        public void Success_string_sets_Outcome_to_Success()
        {
            FetchResult result = FetchResult.Success("{}", "\"etag\"");
            Assert.Equal(FetchOutcome.Success, result.Outcome);
        }

        [Fact]
        public void Success_string_sets_Content_and_ETag()
        {
            FetchResult result = FetchResult.Success("{\"a\":1}", "\"v1\"");
            Assert.Equal("{\"a\":1}", result.Content);
            Assert.Equal("\"v1\"", result.ETag);
        }

        [Fact]
        public void Success_string_leaves_ContentBytes_and_ErrorMessage_null()
        {
            FetchResult result = FetchResult.Success("{}", "\"etag\"");
            Assert.Null(result.ContentBytes);
            Assert.Null(result.ErrorMessage);
        }

        // --- Success(byte[], string) ---------------------------------------------------------------

        [Fact]
        public void Success_bytes_sets_Outcome_to_Success()
        {
            FetchResult result = FetchResult.Success(new byte[] { 1, 2, 3 }, "\"etag\"");
            Assert.Equal(FetchOutcome.Success, result.Outcome);
        }

        [Fact]
        public void Success_bytes_sets_ContentBytes_and_ETag()
        {
            byte[] payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            FetchResult result = FetchResult.Success(payload, "\"v2\"");

            Assert.Same(payload, result.ContentBytes);
            Assert.Equal("\"v2\"", result.ETag);
        }

        [Fact]
        public void Success_bytes_leaves_Content_and_ErrorMessage_null()
        {
            FetchResult result = FetchResult.Success(new byte[] { 1 }, "\"etag\"");
            Assert.Null(result.Content);
            Assert.Null(result.ErrorMessage);
        }

        // --- NotModified ----------------------------------------------------------------------------

        [Fact]
        public void NotModified_sets_Outcome_to_NotModified()
        {
            FetchResult result = FetchResult.NotModified();
            Assert.Equal(FetchOutcome.NotModified, result.Outcome);
        }

        [Fact]
        public void NotModified_leaves_all_other_fields_null()
        {
            FetchResult result = FetchResult.NotModified();
            Assert.Null(result.Content);
            Assert.Null(result.ContentBytes);
            Assert.Null(result.ETag);
            Assert.Null(result.ErrorMessage);
        }

        // --- Failed -----------------------------------------------------------------------------------

        [Fact]
        public void Failed_sets_Outcome_to_Failed()
        {
            FetchResult result = FetchResult.Failed("network unreachable");
            Assert.Equal(FetchOutcome.Failed, result.Outcome);
        }

        [Fact]
        public void Failed_sets_ErrorMessage()
        {
            FetchResult result = FetchResult.Failed("network unreachable");
            Assert.Equal("network unreachable", result.ErrorMessage);
        }

        [Fact]
        public void Failed_leaves_Content_ContentBytes_and_ETag_null()
        {
            FetchResult result = FetchResult.Failed("boom");
            Assert.Null(result.Content);
            Assert.Null(result.ContentBytes);
            Assert.Null(result.ETag);
        }

        // --- plain property round-trip (object initializer surface) --------------------------------

        [Fact]
        public void Properties_round_trip_when_set_directly()
        {
            FetchResult result = new FetchResult
            {
                Outcome = FetchOutcome.Success,
                Content = "x",
                ContentBytes = new byte[] { 9 },
                ETag = "e",
                ErrorMessage = "m",
            };

            Assert.Equal(FetchOutcome.Success, result.Outcome);
            Assert.Equal("x", result.Content);
            Assert.Equal(new byte[] { 9 }, result.ContentBytes);
            Assert.Equal("e", result.ETag);
            Assert.Equal("m", result.ErrorMessage);
        }

        [Fact]
        public void Default_constructor_leaves_Outcome_at_its_default_enum_value()
        {
            // FetchOutcome has no explicit default member; the first declared value (Success) is
            // the implicit 0 — this pins that behaviour so a future reordering is caught.
            FetchResult result = new FetchResult();
            Assert.Equal(FetchOutcome.Success, result.Outcome);
        }
    }
}
