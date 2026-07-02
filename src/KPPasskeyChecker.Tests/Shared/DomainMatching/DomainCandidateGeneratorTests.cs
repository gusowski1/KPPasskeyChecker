using System.Linq;
using KeeRadar.Shared.DomainMatching;
using Xunit;

namespace KPPasskeyChecker.Tests.Shared.DomainMatching
{
    /// <summary>
    /// Architecture-Assessment 2026-07-02, Achse 7 / Rangliste #3 ("PSL-HttpClient auf
    /// statischen Singleton umstellen"). <see cref="DomainCandidateGenerator"/> is touched by that
    /// hardening (its <c>LoadPslAsync</c> creates a per-refresh <c>HttpClient</c>), so per the
    /// "touch it -&gt; test it" ratchet it graduated out of
    /// <c>TestCoverageExemptions.Grandfathered</c> and got real tests — this file is that test class.
    ///
    /// Scope is the pure, network-free eTLD+1 / candidate-generation logic
    /// (<see cref="DomainCandidateGenerator.GetCandidates"/>, portable from
    /// <c>tools\SelfCheck\SharedChecks.CheckDomainCandidatesEtldPlusOne</c>, extended with edge/
    /// negative cases). The HTTP PSL download path (<c>LoadPslAsync</c> / <c>InitializeAsync</c>)
    /// stays excluded from unit testing per Lars' Option-b decision (P-O/F-O, 2026-07-01,
    /// mirrored for Achse 5 in the architecture assessment): no HttpClient mocking, no forced
    /// production seam beyond what the PSL-HttpClient fix itself introduces.
    ///
    /// GetCandidates never touches the network directly and does not require
    /// DomainCandidateGenerator.InitializeAsync to have run — the static _psl field is null by
    /// default, so these tests exercise the 2-label fallback path deterministically.
    /// </summary>
    public class DomainCandidateGeneratorTests
    {
        [Fact]
        public void GetCandidates_yields_full_host_first()
        {
            var candidates = DomainCandidateGenerator.GetCandidates("mail.google.com").ToList();

            Assert.NotEmpty(candidates);
            Assert.Equal("mail.google.com", candidates[0]);
        }

        [Fact]
        public void GetCandidates_walks_down_to_the_two_label_fallback()
        {
            var candidates = DomainCandidateGenerator.GetCandidates("mail.google.com").ToList();

            Assert.Equal(new[] { "mail.google.com", "google.com" }, candidates);
        }

        [Fact]
        public void GetCandidates_strips_a_leading_www_label()
        {
            var candidates = DomainCandidateGenerator.GetCandidates("www.example.com").ToList();

            Assert.Equal(new[] { "example.com" }, candidates);
        }

        [Fact]
        public void GetCandidates_walks_a_deeper_subdomain_chain_most_specific_first()
        {
            var candidates = DomainCandidateGenerator.GetCandidates("a.b.c.example.com").ToList();

            Assert.Equal(
                new[] { "a.b.c.example.com", "b.c.example.com", "c.example.com", "example.com" },
                candidates);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetCandidates_yields_nothing_for_blank_input(string rawHost)
        {
            var candidates = DomainCandidateGenerator.GetCandidates(rawHost).ToList();

            Assert.Empty(candidates);
        }

        [Fact]
        public void GetCandidates_yields_nothing_for_a_single_label_host()
        {
            // labels.Length < 2 -> yield break (no eTLD+1 is reachable from a bare TLD/host).
            var candidates = DomainCandidateGenerator.GetCandidates("localhost").ToList();

            Assert.Empty(candidates);
        }

        [Fact]
        public void GetCandidates_normalizes_case_and_trims_whitespace()
        {
            var candidates = DomainCandidateGenerator.GetCandidates("  WWW.Example.COM  ").ToList();

            Assert.Equal(new[] { "example.com" }, candidates);
        }

        [Fact]
        public void GetCandidates_stops_at_two_labels_when_no_psl_is_loaded()
        {
            // No InitializeAsync call happened in this test (or in this AppDomain run order) that
            // could have published a non-null PSL for "co.uk"-style multi-label public suffixes,
            // so the fallback rule (stop when only one label would remain) governs deterministically.
            var candidates = DomainCandidateGenerator.GetCandidates("shop.example.co.uk").ToList();

            Assert.Equal(
                new[] { "shop.example.co.uk", "example.co.uk", "co.uk" },
                candidates);
        }
    }
}
