using System;
using KPPasskeyChecker.Data;
using Xunit;

namespace KPPasskeyChecker.Tests.Data
{
    /// <summary>
    /// Full public-surface coverage of <see cref="PasskeyDataResult"/> — property round-trip plus
    /// the derived IsSuccess property.
    /// </summary>
    public class PasskeyDataResultTests
    {
        [Fact]
        public void IsSuccess_is_false_when_Directory_is_null()
        {
            var result = new PasskeyDataResult { Directory = null };
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void IsSuccess_is_true_when_Directory_is_set()
        {
            var result = new PasskeyDataResult { Directory = EmptyDirectory() };
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void Default_instance_has_Directory_null_and_IsSuccess_false()
        {
            var result = new PasskeyDataResult();
            Assert.Null(result.Directory);
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void Default_instance_has_default_bool_and_DateTimeOffset_values()
        {
            var result = new PasskeyDataResult();
            Assert.False(result.IsFromCache);
            Assert.False(result.IsStale);
            Assert.Equal(default(DateTimeOffset), result.FetchedAt);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void Properties_round_trip_the_assigned_values()
        {
            var directory = EmptyDirectory();
            var fetchedAt = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

            var result = new PasskeyDataResult
            {
                Directory = directory,
                IsFromCache = true,
                IsStale = true,
                FetchedAt = fetchedAt,
                ErrorMessage = "network unreachable"
            };

            Assert.Same(directory, result.Directory);
            Assert.True(result.IsFromCache);
            Assert.True(result.IsStale);
            Assert.Equal(fetchedAt, result.FetchedAt);
            Assert.Equal("network unreachable", result.ErrorMessage);
        }

        [Fact]
        public void IsSuccess_can_be_true_even_when_ErrorMessage_is_set()
        {
            // IsSuccess only reflects Directory != null; it does not clear/ignore a stale
            // ErrorMessage left over from a prior failed attempt (no cross-field coupling exists
            // in the production type, and none should be assumed by callers).
            var result = new PasskeyDataResult
            {
                Directory = EmptyDirectory(),
                ErrorMessage = "stale message"
            };

            Assert.True(result.IsSuccess);
            Assert.Equal("stale message", result.ErrorMessage);
        }

        // PasskeyDirectory has no public constructor (private ctor + internal static Build) — the
        // internal factory is reachable here because the test assembly is compiled against
        // KPPasskeyChecker's internals (see InternalsVisibleTo in Properties\AssemblyInfo.cs, the
        // same mechanism SelfCheck relies on). An empty raw dictionary yields a valid, empty
        // PasskeyDirectory instance sufficient to prove IsSuccess/round-trip behaviour — the
        // directory's own contents are PasskeyDirectory's concern (grandfathered), not this DTO's.
        private static PasskeyDirectory EmptyDirectory()
        {
            return PasskeyDirectory.Build(new System.Collections.Generic.Dictionary<string, object>());
        }
    }
}
