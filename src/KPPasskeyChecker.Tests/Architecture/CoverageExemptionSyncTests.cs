using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace KPPasskeyChecker.Tests.Architecture
{
    /// <summary>
    /// Keeps the coverlet coverage-collector exclude filter (<c>coverage.runsettings</c>, at the
    /// repository root next to the .sln) in sync with the single source of truth for documented
    /// coverage exemptions, <see cref="TestCoverageExemptions.Entries"/>, so the two lists can
    /// never silently diverge (an exemption added to one list but not the other).
    ///
    /// Direction A: every entry in <see cref="TestCoverageExemptions.Entries"/> must have a
    /// matching "[KPPasskeyChecker]&lt;TypeFullName&gt;" pattern in the runsettings
    /// &lt;Exclude&gt; filter.
    ///
    /// Direction B: every exclude pattern that targets a specific type in the plugin assembly
    /// must correspond to a documented <see cref="TestCoverageExemptions.Entries"/> item — no
    /// silent additional exclusions. Three kinds of pattern are whitelisted and never subject to
    /// Direction B: (1) foreign-assembly patterns (e.g. "[KeePass]*", "[xunit*]*"), and (2)/(3) a
    /// pattern whose named type resolves in the production assembly and is itself structurally
    /// exempt from the needs-tests guard via the Ext-suffix or Form-derivation rule (see
    /// <see cref="TestCoverageExemptions.IsEntrypointExt"/> /
    /// <see cref="TestCoverageExemptions.IsWinFormsFormDerivation"/>) — the coverage exclude is
    /// then justified by the SAME structural rule the needs-tests guard already enforces, rather
    /// than a second, hardcoded per-class list (e.g. KPPasskeyCheckerExt, PasskeySettingsForm).
    /// </summary>
    public class CoverageExemptionSyncTests
    {
        private const string PluginAssemblyName = "KPPasskeyChecker";

        private static readonly Assembly ProductionAssembly =
            typeof(KPPasskeyChecker.KPPasskeyCheckerExt).Assembly;

        private static readonly Regex ExcludeElementPattern =
            new Regex("<Exclude>(.*?)</Exclude>", RegexOptions.Singleline);

        private readonly ITestOutputHelper _output;

        public CoverageExemptionSyncTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ExcludeFilterMatchesDocumentedExemptions()
        {
            string runSettingsPath = LocateRunSettingsPath();

            Assert.True(
                File.Exists(runSettingsPath),
                "coverage.runsettings not found at the expected repository-root location ("
                    + runSettingsPath + "). Create it with a <Exclude> element listing one "
                    + "\"[" + PluginAssemblyName + "]<TypeFullName>\" pattern per "
                    + "TestCoverageExemptions.Entries entry, comma-separated.");

            IReadOnlyList<string> excludePatterns = ParseExcludePatterns(runSettingsPath);

            _output.WriteLine("Documented exemptions (TestCoverageExemptions.Entries):");
            foreach (var entry in TestCoverageExemptions.Entries)
            {
                _output.WriteLine("  - " + entry.TypeFullName);
            }

            _output.WriteLine("Exclude patterns read from " + runSettingsPath + ":");
            foreach (string pattern in excludePatterns)
            {
                _output.WriteLine("  - " + pattern);
            }

            var missing = TestCoverageExemptions.Entries
                .Select(e => "[" + PluginAssemblyName + "]" + e.TypeFullName)
                .Where(expected => !excludePatterns.Contains(expected))
                .ToList();

            Assert.True(
                missing.Count == 0,
                "TestCoverageExemptions.Entries types missing from the coverage.runsettings "
                    + "<Exclude> filter: " + string.Join(", ", missing));

            var documented = new HashSet<string>(
                TestCoverageExemptions.Entries
                    .Select(e => "[" + PluginAssemblyName + "]" + e.TypeFullName));

            var undocumented = excludePatterns
                .Where(IsSubjectToDirectionB)
                .Where(p => !documented.Contains(p))
                .Where(p => !IsStructurallyExemptPattern(p))
                .ToList();

            Assert.True(
                undocumented.Count == 0,
                "coverage.runsettings excludes types that are not documented in "
                    + "TestCoverageExemptions.Entries and are not structurally exempt (Ext-suffix "
                    + "or Form-derivation) either (remove the pattern or add a reasoned entry): "
                    + string.Join(", ", undocumented));
        }

        /// <summary>
        /// True for an exclude pattern that must correspond 1:1 to a
        /// <see cref="TestCoverageExemptions.Entries"/> item (Direction B). Patterns targeting a
        /// different assembly (e.g. "[KeePass]*", "[xunit*]*") are structural, foreign-assembly
        /// excludes and are never subject to Direction B.
        /// </summary>
        private static bool IsSubjectToDirectionB(string pattern)
        {
            string prefix = "[" + PluginAssemblyName + "]";
            return pattern.StartsWith(prefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// True if <paramref name="pattern"/> names a type that resolves in the production
        /// assembly AND is structurally exempt from the needs-tests guard via the Ext-suffix or
        /// Form-derivation rule (see class remarks). A pattern whose type cannot be resolved, or
        /// that resolves to a type covered by neither rule, is NOT structurally exempt and must
        /// instead be a documented <see cref="TestCoverageExemptions.Entries"/> item.
        /// </summary>
        private static bool IsStructurallyExemptPattern(string pattern)
        {
            string prefix = "[" + PluginAssemblyName + "]";
            if (!pattern.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            string typeFullName = pattern.Substring(prefix.Length);
            Type type = ProductionAssembly.GetType(typeFullName);
            if (type == null)
            {
                return false;
            }

            return TestCoverageExemptions.IsEntrypointExt(type)
                || TestCoverageExemptions.IsWinFormsFormDerivation(type);
        }

        private static IReadOnlyList<string> ParseExcludePatterns(string runSettingsPath)
        {
            string content = File.ReadAllText(runSettingsPath);
            Match match = ExcludeElementPattern.Match(content);

            Assert.True(
                match.Success,
                "coverage.runsettings has no <Exclude>...</Exclude> element under "
                    + "DataCollectionRunSettings/DataCollectors/DataCollector/Configuration.");

            return match.Groups[1].Value
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        /// <summary>
        /// Climbs up from the test assembly's output directory to the repository root (the
        /// directory containing the .sln), mirroring the path-ascent pattern already used
        /// elsewhere in this test project for locating on-disk state relative to the test binary,
        /// and returns the expected coverage.runsettings path there.
        /// </summary>
        private static string LocateRunSettingsPath()
        {
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 10 && !string.IsNullOrEmpty(dir); i++)
            {
                if (Directory.GetFiles(dir, "*.sln").Length > 0)
                {
                    return Path.Combine(dir, "coverage.runsettings");
                }

                dir = Path.GetDirectoryName(
                    dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }

            throw new InvalidOperationException(
                "Could not locate the repository root (a directory containing a .sln file) by "
                    + "walking up from " + AppContext.BaseDirectory);
        }
    }
}
