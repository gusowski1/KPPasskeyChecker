using System;
using KeeRadar.Shared.Caching;
using Xunit;

namespace KPPasskeyChecker.Tests.Shared.Caching
{
    /// <summary>
    /// Full public-surface coverage of <see cref="CacheEntry"/> — constructor default plus plain
    /// property round-trip for all three members.
    /// Ownership: <c>KeeRadar.Shared.*</c> is tested exclusively in KPPasskeyChecker.Tests (the
    /// canonical source); KP2FAChecker.Tests excludes the whole namespace.
    /// </summary>
    public class CacheEntryTests
    {
        [Fact]
        public void Default_constructor_sets_Content_to_empty_string_not_null()
        {
            CacheEntry entry = new CacheEntry();
            Assert.Equal(string.Empty, entry.Content);
        }

        [Fact]
        public void Default_constructor_leaves_ETag_null()
        {
            CacheEntry entry = new CacheEntry();
            Assert.Null(entry.ETag);
        }

        [Fact]
        public void Default_constructor_leaves_FetchedAt_at_its_default_value()
        {
            CacheEntry entry = new CacheEntry();
            Assert.Equal(default(DateTimeOffset), entry.FetchedAt);
        }

        [Fact]
        public void Content_round_trips_an_assigned_value()
        {
            CacheEntry entry = new CacheEntry { Content = "{\"a\":1}" };
            Assert.Equal("{\"a\":1}", entry.Content);
        }

        [Fact]
        public void Content_can_be_set_back_to_null()
        {
            CacheEntry entry = new CacheEntry { Content = "x" };
            entry.Content = null;
            Assert.Null(entry.Content);
        }

        [Fact]
        public void ETag_round_trips_an_assigned_value()
        {
            CacheEntry entry = new CacheEntry { ETag = "\"abc123\"" };
            Assert.Equal("\"abc123\"", entry.ETag);
        }

        [Fact]
        public void FetchedAt_round_trips_an_assigned_value()
        {
            DateTimeOffset now = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);
            CacheEntry entry = new CacheEntry { FetchedAt = now };
            Assert.Equal(now, entry.FetchedAt);
        }

        [Fact]
        public void Properties_are_independent_of_each_other()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            CacheEntry entry = new CacheEntry
            {
                Content = "payload",
                ETag = "etag-value",
                FetchedAt = now,
            };

            Assert.Equal("payload", entry.Content);
            Assert.Equal("etag-value", entry.ETag);
            Assert.Equal(now, entry.FetchedAt);
        }
    }
}
