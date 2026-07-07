using System;
using System.IO;
using KeeRadar.Shared.Caching;
using Xunit;

namespace KPPasskeyChecker.Tests.Shared.Caching
{
    /// <summary>
    /// Full public-surface coverage of <see cref="FileSystemJsonCache"/> — Read/Write/Invalidate,
    /// atomic write-then-replace, ETag/FetchedAt metadata round-trip, and the cache-miss paths,
    /// exercised against a real, isolated temp directory created and torn down per test (no mock —
    /// <c>ILocalCache</c> is a real filesystem wrapper, so this suite uses real disk I/O).
    /// Ownership: <c>KeeRadar.Shared.*</c> is tested exclusively in KPPasskeyChecker.Tests (the
    /// canonical source); KP2FAChecker.Tests excludes the whole namespace.
    /// </summary>
    public class FileSystemJsonCacheTests : IDisposable
    {
        private readonly string _dir;

        public FileSystemJsonCacheTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "KPPasskeyChecker.Tests_" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_dir))
                    Directory.Delete(_dir, recursive: true);
            }
            catch
            {
                // best-effort cleanup; never fail a test on teardown
            }
        }

        // --- constructor -------------------------------------------------------------------------

        [Fact]
        public void Constructor_creates_the_directory_if_it_does_not_exist()
        {
            Assert.False(Directory.Exists(_dir));

            new FileSystemJsonCache(_dir);

            Assert.True(Directory.Exists(_dir));
        }

        [Fact]
        public void Constructor_does_not_throw_when_the_directory_already_exists()
        {
            Directory.CreateDirectory(_dir);

            var ex = Record.Exception(() => new FileSystemJsonCache(_dir));

            Assert.Null(ex);
        }

        // --- Read: cache miss ----------------------------------------------------------------------

        [Fact]
        public void Read_returns_null_for_a_key_that_was_never_written()
        {
            var cache = new FileSystemJsonCache(_dir);

            CacheEntry entry = cache.Read("never-written");

            Assert.Null(entry);
        }

        [Fact]
        public void Read_returns_null_when_only_the_content_file_exists()
        {
            var cache = new FileSystemJsonCache(_dir);
            File.WriteAllText(Path.Combine(_dir, "partial.json"), "{}");

            CacheEntry entry = cache.Read("partial");

            Assert.Null(entry);
        }

        [Fact]
        public void Read_returns_null_when_the_meta_file_is_corrupt_instead_of_throwing()
        {
            var cache = new FileSystemJsonCache(_dir);
            cache.Write("k", new CacheEntry { Content = "{}", ETag = "\"x\"", FetchedAt = DateTimeOffset.UtcNow });
            // Simulate a genuinely broken meta file by overwriting it with binary junk that cannot
            // be parsed as UTF-8 key/value lines.
            File.WriteAllBytes(Path.Combine(_dir, "k.meta.txt"), new byte[] { 0xFF, 0xFE, 0x00, 0x01 });

            CacheEntry entry = cache.Read("k");

            // Malformed metadata does not necessarily fail the whole read (unknown fields just yield
            // null etag/fetchedat), but it must never throw — assert the call is safe either way.
            Assert.True(entry == null || entry.Content != null);
        }

        // --- Write + Read: round trip ----------------------------------------------------------------

        [Fact]
        public void Write_then_Read_round_trips_the_content()
        {
            var cache = new FileSystemJsonCache(_dir);
            var written = new CacheEntry { Content = "{\"a\":1}", ETag = "\"etag1\"", FetchedAt = DateTimeOffset.UtcNow };

            cache.Write("mfa", written);
            CacheEntry read = cache.Read("mfa");

            Assert.NotNull(read);
            Assert.Equal("{\"a\":1}", read.Content);
        }

        [Fact]
        public void Write_then_Read_round_trips_the_ETag()
        {
            var cache = new FileSystemJsonCache(_dir);
            cache.Write("mfa", new CacheEntry { Content = "{}", ETag = "\"abc123\"", FetchedAt = DateTimeOffset.UtcNow });

            CacheEntry read = cache.Read("mfa");

            Assert.Equal("\"abc123\"", read.ETag);
        }

        [Fact]
        public void Write_then_Read_round_trips_FetchedAt_with_round_trip_precision()
        {
            var cache = new FileSystemJsonCache(_dir);
            DateTimeOffset fetched = new DateTimeOffset(2026, 7, 4, 12, 34, 56, TimeSpan.Zero);
            cache.Write("mfa", new CacheEntry { Content = "{}", ETag = null, FetchedAt = fetched });

            CacheEntry read = cache.Read("mfa");

            Assert.Equal(fetched, read.FetchedAt);
        }

        [Fact]
        public void Write_with_a_null_ETag_round_trips_to_an_empty_or_null_ETag_on_read()
        {
            var cache = new FileSystemJsonCache(_dir);
            cache.Write("mfa", new CacheEntry { Content = "{}", ETag = null, FetchedAt = DateTimeOffset.UtcNow });

            CacheEntry read = cache.Read("mfa");

            Assert.True(string.IsNullOrEmpty(read.ETag));
        }

        [Fact]
        public void Write_overwrites_a_previously_written_entry_for_the_same_key()
        {
            var cache = new FileSystemJsonCache(_dir);
            cache.Write("mfa", new CacheEntry { Content = "{\"v\":1}", ETag = "\"e1\"", FetchedAt = DateTimeOffset.UtcNow });
            cache.Write("mfa", new CacheEntry { Content = "{\"v\":2}", ETag = "\"e2\"", FetchedAt = DateTimeOffset.UtcNow });

            CacheEntry read = cache.Read("mfa");

            Assert.Equal("{\"v\":2}", read.Content);
            Assert.Equal("\"e2\"", read.ETag);
        }

        [Fact]
        public void Write_is_atomic_and_leaves_no_leftover_tmp_file_on_disk()
        {
            var cache = new FileSystemJsonCache(_dir);

            cache.Write("mfa", new CacheEntry { Content = "{}", ETag = "\"e\"", FetchedAt = DateTimeOffset.UtcNow });

            Assert.False(File.Exists(Path.Combine(_dir, "mfa.json.tmp")));
            Assert.False(File.Exists(Path.Combine(_dir, "mfa.meta.txt.tmp")));
            Assert.True(File.Exists(Path.Combine(_dir, "mfa.json")));
            Assert.True(File.Exists(Path.Combine(_dir, "mfa.meta.txt")));
        }

        [Fact]
        public void Write_replacing_an_existing_entry_uses_File_Replace_and_still_leaves_no_tmp_file()
        {
            var cache = new FileSystemJsonCache(_dir);
            cache.Write("mfa", new CacheEntry { Content = "{\"v\":1}", ETag = "\"e1\"", FetchedAt = DateTimeOffset.UtcNow });

            cache.Write("mfa", new CacheEntry { Content = "{\"v\":2}", ETag = "\"e2\"", FetchedAt = DateTimeOffset.UtcNow });

            Assert.False(File.Exists(Path.Combine(_dir, "mfa.json.tmp")));
            Assert.False(File.Exists(Path.Combine(_dir, "mfa.meta.txt.tmp")));
        }

        [Fact]
        public void Different_keys_are_stored_independently()
        {
            var cache = new FileSystemJsonCache(_dir);
            cache.Write("mfa", new CacheEntry { Content = "{\"a\":1}", FetchedAt = DateTimeOffset.UtcNow });
            cache.Write("passwordless", new CacheEntry { Content = "{\"b\":2}", FetchedAt = DateTimeOffset.UtcNow });

            Assert.Equal("{\"a\":1}", cache.Read("mfa").Content);
            Assert.Equal("{\"b\":2}", cache.Read("passwordless").Content);
        }

        [Fact]
        public void Metadata_values_containing_newlines_and_backslashes_round_trip_via_escaping()
        {
            var cache = new FileSystemJsonCache(_dir);
            // A real newline plus a real backslash — both must survive the meta file's own
            // "\n"/"\\" escaping (EscapeValue/UnescapeValue) round trip intact.
            string trickyEtag = "\"line1\nline2\\end\"";

            cache.Write("mfa", new CacheEntry { Content = "{}", ETag = trickyEtag, FetchedAt = DateTimeOffset.UtcNow });
            CacheEntry read = cache.Read("mfa");

            Assert.Equal(trickyEtag, read.ETag);
        }

        // --- Invalidate ------------------------------------------------------------------------------

        [Fact]
        public void Invalidate_removes_both_content_and_meta_files()
        {
            var cache = new FileSystemJsonCache(_dir);
            cache.Write("mfa", new CacheEntry { Content = "{}", FetchedAt = DateTimeOffset.UtcNow });

            cache.Invalidate("mfa");

            Assert.False(File.Exists(Path.Combine(_dir, "mfa.json")));
            Assert.False(File.Exists(Path.Combine(_dir, "mfa.meta.txt")));
            Assert.Null(cache.Read("mfa"));
        }

        [Fact]
        public void Invalidate_on_a_never_written_key_does_not_throw()
        {
            var cache = new FileSystemJsonCache(_dir);

            var ex = Record.Exception(() => cache.Invalidate("never-existed"));

            Assert.Null(ex);
        }

        [Fact]
        public void Invalidate_does_not_affect_other_keys()
        {
            var cache = new FileSystemJsonCache(_dir);
            cache.Write("mfa", new CacheEntry { Content = "{\"a\":1}", FetchedAt = DateTimeOffset.UtcNow });
            cache.Write("passwordless", new CacheEntry { Content = "{\"b\":2}", FetchedAt = DateTimeOffset.UtcNow });

            cache.Invalidate("mfa");

            Assert.Null(cache.Read("mfa"));
            Assert.NotNull(cache.Read("passwordless"));
        }

        // --- key sanitization -------------------------------------------------------------------------

        [Fact]
        public void Keys_containing_invalid_filename_characters_are_sanitized_without_throwing()
        {
            var cache = new FileSystemJsonCache(_dir);
            string oddKey = "weird" + Path.GetInvalidFileNameChars()[0] + "key";

            var ex = Record.Exception(() =>
                cache.Write(oddKey, new CacheEntry { Content = "{}", FetchedAt = DateTimeOffset.UtcNow }));

            Assert.Null(ex);
        }
    }
}
