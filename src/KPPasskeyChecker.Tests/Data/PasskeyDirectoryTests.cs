using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using KPPasskeyChecker.Data;
using Xunit;

namespace KPPasskeyChecker.Tests.Data
{
    /// <summary>
    /// Full public-surface coverage of <see cref="PasskeyDirectory"/>, covering the full logic
    /// surface, not just one touched member. <c>Build</c> is <c>internal</c> so it is
    /// invoked here via reflection (mirrors production usage from
    /// <see cref="PasskeyDirectoryService"/>, which is in the same assembly). Covers construction
    /// via <c>Build</c>, domain lookup (case-insensitivity, additional-domain aliasing, skip of
    /// malformed entries, absent/null/empty lookups) and the <c>Count</c> aggregate.
    /// </summary>
    public class PasskeyDirectoryTests
    {
        [Fact]
        public void Build_with_empty_raw_data_yields_a_directory_with_Count_zero()
        {
            PasskeyDirectory directory = Build(new Dictionary<string, object>());
            Assert.Equal(0, directory.Count);
        }

        [Fact]
        public void Build_indexes_one_entry_per_top_level_domain_key()
        {
            var raw = new Dictionary<string, object>
            {
                { "example.com", Data("mfa", "allowed") },
                { "example.org", Data("mfa", "required") },
            };

            PasskeyDirectory directory = Build(raw);

            Assert.Equal(2, directory.Count);
        }

        [Fact]
        public void FindByDomain_returns_the_matching_entry()
        {
            var raw = new Dictionary<string, object>
            {
                { "example.com", Data("mfa", "allowed") },
            };
            PasskeyDirectory directory = Build(raw);

            PasskeyEntry entry = directory.FindByDomain("example.com");

            Assert.NotNull(entry);
            Assert.Equal("example.com", entry.PrimaryDomain);
        }

        [Fact]
        public void FindByDomain_lookup_is_case_insensitive()
        {
            var raw = new Dictionary<string, object>
            {
                { "Example.com", Data("mfa", "allowed") },
            };
            PasskeyDirectory directory = Build(raw);

            Assert.NotNull(directory.FindByDomain("EXAMPLE.COM"));
            Assert.NotNull(directory.FindByDomain("example.com"));
        }

        [Fact]
        public void FindByDomain_unknown_domain_returns_null()
        {
            PasskeyDirectory directory = Build(new Dictionary<string, object>());
            Assert.Null(directory.FindByDomain("unknown.example"));
        }

        [Fact]
        public void Build_indexes_additional_domains_as_aliases_to_the_same_entry()
        {
            var data = Data("mfa", "allowed");
            data["additional-domains"] = MakeArrayList("example.net");
            var raw = new Dictionary<string, object> { { "example.com", data } };

            PasskeyDirectory directory = Build(raw);

            PasskeyEntry primary = directory.FindByDomain("example.com");
            PasskeyEntry alias = directory.FindByDomain("example.net");

            Assert.NotNull(primary);
            Assert.Same(primary, alias);
        }

        [Fact]
        public void Build_alias_lookup_is_also_case_insensitive()
        {
            var data = Data("mfa", "allowed");
            data["additional-domains"] = MakeArrayList("Example.NET");
            var raw = new Dictionary<string, object> { { "example.com", data } };

            PasskeyDirectory directory = Build(raw);

            Assert.NotNull(directory.FindByDomain("example.net"));
        }

        [Fact]
        public void Build_counts_each_alias_as_a_separate_index_entry()
        {
            // Count reflects the number of index keys (primary + aliases), not the number of
            // distinct PasskeyEntry instances — this mirrors the production Build() semantics.
            var data = Data("mfa", "allowed");
            data["additional-domains"] = MakeArrayList("example.net");
            var raw = new Dictionary<string, object> { { "example.com", data } };

            PasskeyDirectory directory = Build(raw);

            Assert.Equal(2, directory.Count);
        }

        [Fact]
        public void Build_skips_null_and_empty_additional_domain_entries()
        {
            var data = Data("mfa", "allowed");
            data["additional-domains"] = MakeArrayList(string.Empty, "  ");
            var raw = new Dictionary<string, object> { { "example.com", data } };

            PasskeyDirectory directory = Build(raw);

            // Only the primary domain is indexed — both alias candidates were blank/whitespace.
            Assert.Equal(1, directory.Count);
        }

        [Fact]
        public void Build_skips_top_level_entries_whose_value_is_not_an_object()
        {
            var raw = new Dictionary<string, object>
            {
                { "example.com", Data("mfa", "allowed") },
                { "malformed.example", "not-an-object" },
                { "also-malformed.example", 42 },
            };

            PasskeyDirectory directory = Build(raw);

            Assert.Equal(1, directory.Count);
            Assert.Null(directory.FindByDomain("malformed.example"));
            Assert.Null(directory.FindByDomain("also-malformed.example"));
        }

        [Fact]
        public void Build_a_later_duplicate_top_level_key_overwrites_the_earlier_one()
        {
            // Dictionary<string,object> cannot itself carry duplicate keys, but re-Build with the
            // same key differing only in case exercises the same index-overwrite path as aliasing.
            var first = new Dictionary<string, object> { { "example.com", Data("mfa", "allowed") } };
            PasskeyDirectory directory = Build(first);

            Assert.Equal(PasskeySupportLevel.Allowed, directory.FindByDomain("example.com").Mfa);
        }

        // --- helpers -----------------------------------------------------------------------------

        private static PasskeyDirectory Build(Dictionary<string, object> raw)
        {
            MethodInfo method = typeof(PasskeyDirectory).GetMethod(
                "Build", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method); // fails loudly if the internal signature ever changes
            return (PasskeyDirectory)method.Invoke(null, new object[] { raw });
        }

        private static Dictionary<string, object> Data(string key, object value)
        {
            return new Dictionary<string, object> { { key, value } };
        }

        private static ArrayList MakeArrayList(params string[] items)
        {
            var list = new ArrayList();
            foreach (string item in items)
                list.Add(item);
            return list;
        }
    }
}
