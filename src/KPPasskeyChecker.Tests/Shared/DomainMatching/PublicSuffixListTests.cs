using KeeRadar.Shared.DomainMatching;
using Xunit;

namespace KPPasskeyChecker.Tests.Shared.DomainMatching
{
    /// <summary>
    /// See <see cref="DomainCandidateGeneratorTests"/> for the ratchet rationale.
    /// <see cref="PublicSuffixList"/> is the
    /// PSL parser/lookup used by <see cref="DomainCandidateGenerator"/>; it is pure, network-free
    /// logic (parsing a PSL text fixture and resolving eTLD+1), portable from
    /// <c>tools\SelfCheck\SharedChecks.CheckDomainCandidatesEtldPlusOne</c>, extended here.
    /// </summary>
    public class PublicSuffixListTests
    {
        private static PublicSuffixList Fixture()
        {
            return PublicSuffixList.Parse(
                "// test fixture\n" +
                "com\n" +
                "co.uk\n" +
                "uk\n" +
                "*.kawasaki.jp\n" +
                "!city.kawasaki.jp\n");
        }

        [Fact]
        public void GetRegistrableDomain_resolves_a_simple_com_suffix()
        {
            Assert.Equal("google.com", Fixture().GetRegistrableDomain("mail.google.com"));
        }

        [Fact]
        public void GetRegistrableDomain_resolves_a_two_label_public_suffix()
        {
            Assert.Equal("example.co.uk", Fixture().GetRegistrableDomain("www.example.co.uk"));
        }

        [Fact]
        public void GetRegistrableDomain_applies_a_wildcard_rule()
        {
            // *.kawasaki.jp -> eTLD is "foo.kawasaki.jp" for any foo, so eTLD+1 needs one more label.
            Assert.Equal("sub.foo.kawasaki.jp", Fixture().GetRegistrableDomain("a.sub.foo.kawasaki.jp"));
        }

        [Fact]
        public void GetRegistrableDomain_applies_an_exception_rule_that_shortens_the_wildcard()
        {
            // !city.kawasaki.jp carves out an exception: eTLD is "kawasaki.jp" (one label shorter
            // than the wildcard match), so eTLD+1 is "city.kawasaki.jp" itself.
            Assert.Equal("city.kawasaki.jp", Fixture().GetRegistrableDomain("www.city.kawasaki.jp"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetRegistrableDomain_returns_null_for_blank_input(string hostname)
        {
            Assert.Null(Fixture().GetRegistrableDomain(hostname));
        }

        [Fact]
        public void GetRegistrableDomain_returns_null_for_a_single_label_host()
        {
            Assert.Null(Fixture().GetRegistrableDomain("localhost"));
        }

        [Fact]
        public void GetRegistrableDomain_returns_null_when_the_hostname_is_itself_a_public_suffix()
        {
            // "co.uk" is itself an exact PSL rule; there is no further label to form eTLD+1.
            Assert.Null(Fixture().GetRegistrableDomain("co.uk"));
        }

        [Fact]
        public void GetRegistrableDomain_falls_back_to_the_rightmost_label_when_no_rule_matches()
        {
            var psl = PublicSuffixList.Parse("// empty fixture, no rules\n");

            Assert.Equal("example.zz", psl.GetRegistrableDomain("sub.example.zz"));
        }

        [Fact]
        public void GetRegistrableDomain_is_case_insensitive()
        {
            Assert.Equal("google.com", Fixture().GetRegistrableDomain("MAIL.GOOGLE.COM"));
        }
    }
}
