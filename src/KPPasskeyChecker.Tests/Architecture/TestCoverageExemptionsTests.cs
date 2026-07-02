using System.Collections.Generic;
using System.Linq;
using ArchUnitNET.Loader;
using Xunit;
using Xunit.Abstractions;

namespace KPPasskeyChecker.Tests.Architecture
{
    /// <summary>
    /// Scenario 9 + technical-debt visibility: exemptions and the grandfathered baseline must be
    /// visible (reported in test output with their reason) and must not go stale. The baseline is
    /// a ratchet — a grandfathered class that gains a test must leave the baseline.
    /// </summary>
    public class TestCoverageExemptionsTests
    {
        private readonly ITestOutputHelper _output;

        public TestCoverageExemptionsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static HashSet<string> LoadedTypeFullNames()
        {
            var architecture = new ArchLoader()
                .LoadAssemblies(typeof(KPPasskeyChecker.KPPasskeyCheckerExt).Assembly)
                .Build();
            return new HashSet<string>(architecture.Types.Select(t => t.FullName));
        }

        [Fact]
        public void ExemptionsAreDocumentedAndReported()
        {
            var allTypeFullNames = LoadedTypeFullNames();

            _output.WriteLine("Explicit exemptions (permanent, with reason):");
            foreach (var entry in TestCoverageExemptions.Entries)
            {
                _output.WriteLine("  - " + entry.TypeFullName + " : " + entry.Reason);
            }

            var undocumented = TestCoverageExemptions.Entries
                .Where(e => string.IsNullOrWhiteSpace(e.Reason))
                .Select(e => e.TypeFullName)
                .ToList();

            var stale = TestCoverageExemptions.Entries
                .Where(e => !allTypeFullNames.Contains(e.TypeFullName))
                .Select(e => e.TypeFullName)
                .ToList();

            Assert.True(
                undocumented.Count == 0,
                "Exemption entries with no (non-empty) reason: " + string.Join(", ", undocumented));

            Assert.True(
                stale.Count == 0,
                "Exempted types that no longer exist (remove the stale exemption): "
                    + string.Join(", ", stale));
        }

        [Fact]
        public void GrandfatheredBaselineIsReportedAndOnlyShrinks()
        {
            var allTypeFullNames = LoadedTypeFullNames();
            var testTypeNames = new HashSet<string>(
                typeof(TestCoverageExemptionsTests).Assembly.GetTypes().Select(t => t.Name));

            _output.WriteLine(
                "Grandfathered pre-0.5.0 classes without tests (technical debt; full coverage "
                + "tracked in backlog Story P-O):");
            foreach (var fullName in TestCoverageExemptions.Grandfathered)
            {
                _output.WriteLine("  - " + fullName);
            }
            _output.WriteLine(
                "Remaining technical-debt classes: " + TestCoverageExemptions.Grandfathered.Count);

            var stale = TestCoverageExemptions.Grandfathered
                .Where(fn => !allTypeFullNames.Contains(fn))
                .ToList();

            // Ratchet: once a grandfathered class has a <Name>Tests, it must leave the baseline.
            var graduated = TestCoverageExemptions.Grandfathered
                .Where(fn => testTypeNames.Contains(SimpleName(fn) + "Tests"))
                .ToList();

            Assert.True(
                stale.Count == 0,
                "Grandfathered types that no longer exist (remove them from the baseline): "
                    + string.Join(", ", stale));

            Assert.True(
                graduated.Count == 0,
                "Grandfathered classes that now HAVE a test — remove them from the baseline so the "
                + "ratchet stays honest: " + string.Join(", ", graduated));
        }

        private static string SimpleName(string fullName)
        {
            int i = fullName.LastIndexOf('.');
            return i < 0 ? fullName : fullName.Substring(i + 1);
        }
    }
}
