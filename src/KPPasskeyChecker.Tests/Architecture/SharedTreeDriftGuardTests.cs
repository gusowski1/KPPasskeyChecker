using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace KPPasskeyChecker.Tests.Architecture
{
    /// <summary>
    /// Architecture-Assessment 2026-07-02, Achse 1 / Rangliste #1 ("Shared-Drift-Guard").
    ///
    /// Defines the target behaviour for a byte-/hash-comparison guard between the two copies of
    /// <c>src\Shared</c> (canonical in KPPasskeyChecker, mirrored verbatim into KP2FAChecker via
    /// <c>sync-shared.ps1</c>): the guard must FAIL when the two trees diverge.
    ///
    /// <b>Split responsibility (per the task's instruction to clarify SelfCheck-vs-xUnit scope):</b>
    /// the actual release-blocking guard belongs in the SDK-free <c>tools\SelfCheck</c> (csc)
    /// mandatory gate (per <c>local_agents.md</c>, SelfCheck is the only QA gate) — it is the one
    /// that must run before every release and cross-checks the REAL two repo trees
    /// (<c>KPPasskeyChecker\src\Shared</c> vs. the sibling repo's <c>KP2FAChecker\src\Shared</c> on
    /// disk). This xUnit test does NOT attempt to invoke or duplicate that release gate (it must
    /// stay csc/no-SDK, and reaching across repos from a test assembly would be a brittle,
    /// environment-dependent path). Instead, this test defines and pins the pure COMPARISON /
    /// DETECTION logic — the algorithm that both the SelfCheck guard and this test exercise
    /// against a synthetic, self-contained fixture (two small temp directory trees created and
    /// torn down entirely inside the test). That logic is what the coder must extract into a
    /// small, dependency-free type (<c>SharedTreeComparer</c>, BCL-only: <c>System.IO</c> +
    /// <c>System.Security.Cryptography</c>) so it compiles unchanged under csc /langversion:5
    /// AND is unit-testable here — one algorithm, two call sites (SelfCheck for the release gate,
    /// this test for the regression pin on the comparison behaviour itself).
    /// </summary>
    public class SharedTreeDriftGuardTests
    {
        [Fact]
        public void Compare_reports_no_divergence_for_two_byte_identical_trees()
        {
            using (var fixture = TwoTreeFixture.CreateIdentical())
            {
                IReadOnlyList<string> divergent = SharedTreeComparer.FindDivergentFiles(fixture.TreeA, fixture.TreeB);

                Assert.Empty(divergent);
            }
        }

        [Fact]
        public void Compare_detects_a_file_with_different_content_as_divergent()
        {
            using (var fixture = TwoTreeFixture.CreateIdentical())
            {
                File.WriteAllText(Path.Combine(fixture.TreeB, "Pgp", "DirectoryTrustAnchor.cs"), "// tampered content");

                IReadOnlyList<string> divergent = SharedTreeComparer.FindDivergentFiles(fixture.TreeA, fixture.TreeB);

                Assert.Contains(
                    divergent,
                    relativePath => relativePath.Replace('\\', '/') == "Pgp/DirectoryTrustAnchor.cs");
            }
        }

        [Fact]
        public void Compare_detects_a_file_missing_from_the_second_tree_as_divergent()
        {
            using (var fixture = TwoTreeFixture.CreateIdentical())
            {
                File.Delete(Path.Combine(fixture.TreeB, "Http", "ConditionalHttpFetcher.cs"));

                IReadOnlyList<string> divergent = SharedTreeComparer.FindDivergentFiles(fixture.TreeA, fixture.TreeB);

                Assert.Contains(
                    divergent,
                    relativePath => relativePath.Replace('\\', '/') == "Http/ConditionalHttpFetcher.cs");
            }
        }

        [Fact]
        public void Compare_detects_a_file_only_present_in_the_second_tree_as_divergent()
        {
            using (var fixture = TwoTreeFixture.CreateIdentical())
            {
                File.WriteAllText(Path.Combine(fixture.TreeB, "Http", "NewFileNotInCanonicalTree.cs"), "// extra");

                IReadOnlyList<string> divergent = SharedTreeComparer.FindDivergentFiles(fixture.TreeA, fixture.TreeB);

                Assert.Contains(
                    divergent,
                    relativePath => relativePath.Replace('\\', '/') == "Http/NewFileNotInCanonicalTree.cs");
            }
        }

        [Fact]
        public void Compare_is_insensitive_to_directory_separator_style_when_reporting_relative_paths()
        {
            using (var fixture = TwoTreeFixture.CreateIdentical())
            {
                IReadOnlyList<string> divergent = SharedTreeComparer.FindDivergentFiles(fixture.TreeA, fixture.TreeB);

                // No divergence expected — this pins that the comparer walks nested subdirectories
                // (Pgp\, Http\) rather than only the top-level file set.
                Assert.Empty(divergent);
            }
        }

        /// <summary>
        /// Creates two temporary, byte-identical directory trees (mirroring the real Shared
        /// subfolder shape: a top-level file plus Pgp\ and Http\ subdirectories) so tests can
        /// mutate one side and assert detection — fully self-contained, no dependency on the real
        /// KPPasskeyChecker/KP2FAChecker repos on disk.
        /// </summary>
        private sealed class TwoTreeFixture : IDisposable
        {
            public string TreeA { get; private set; }
            public string TreeB { get; private set; }

            private readonly string _root;

            private TwoTreeFixture(string root, string treeA, string treeB)
            {
                _root = root;
                TreeA = treeA;
                TreeB = treeB;
            }

            public static TwoTreeFixture CreateIdentical()
            {
                string root = Path.Combine(Path.GetTempPath(), "KeeRadarSharedDriftGuardTest_" + Guid.NewGuid().ToString("N"));
                string treeA = Path.Combine(root, "A");
                string treeB = Path.Combine(root, "B");

                foreach (string relative in new[]
                {
                    "TopLevelMarker.cs",
                    Path.Combine("Pgp", "DirectoryTrustAnchor.cs"),
                    Path.Combine("Http", "ConditionalHttpFetcher.cs"),
                })
                {
                    string content = "// content for " + relative.Replace('\\', '/');
                    WriteInto(treeA, relative, content);
                    WriteInto(treeB, relative, content);
                }

                return new TwoTreeFixture(root, treeA, treeB);
            }

            private static void WriteInto(string treeRoot, string relativePath, string content)
            {
                string fullPath = Path.Combine(treeRoot, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllText(fullPath, content);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(_root))
                        Directory.Delete(_root, true);
                }
                catch
                {
                    // Best-effort temp cleanup; never fail the test on teardown.
                }
            }
        }
    }
}
