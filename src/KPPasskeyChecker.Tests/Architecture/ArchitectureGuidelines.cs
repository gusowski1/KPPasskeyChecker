using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Xunit;
using Type = System.Type;
using Assembly = System.Reflection.Assembly;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace KPPasskeyChecker.Tests.Architecture
{
    /// <summary>
    /// Architecture conformance guard for KPPasskeyChecker (Story P-N).
    ///
    /// Two rules:
    ///   1. Needs-Tests-Guard: every production, non-exempt class in KPPasskeyChecker.* and
    ///      KeeRadar.Shared.* must have a corresponding &lt;Class&gt;Tests class in this test
    ///      assembly (which owns both the plugin's own tests and the Shared tests — P-N decision 3).
    ///   2. Layering hardening: KeeRadar.Shared.* must never depend on KPPasskeyChecker.* (P-N
    ///      decision 4 / Scenario 10).
    /// </summary>
    public class ArchitectureGuidelinesTests
    {
        // The plugin assembly has Shared compiled in (glob include in KPPasskeyChecker.csproj),
        // so it alone covers both KPPasskeyChecker.* and KeeRadar.Shared.*.
        private static readonly Assembly ProductionAssembly =
            typeof(KPPasskeyChecker.KPPasskeyCheckerExt).Assembly;

        // ArchUnit reads IL via Cecil, so the layering rule does not need KeePass.exe resolved
        // at runtime — more robust than reflection for dependency analysis.
        private static readonly ArchUnitNET.Domain.Architecture Architecture =
            new ArchLoader().LoadAssemblies(ProductionAssembly).Build();

        /// <summary>
        /// Scenario 3/4/5: every production, non-exempt class in KPPasskeyChecker.* or
        /// KeeRadar.Shared.* must have a corresponding &lt;Class&gt;Tests class in this test
        /// assembly. Uses reflection (full System.Type surface) rather than the ArchUnit domain
        /// model so that exemptions can reason about base types (WinForms), enums, delegates, etc.
        /// </summary>
        [Fact]
        public void Every_production_class_has_a_corresponding_test_class()
        {
            var testTypeNames = new HashSet<string>(
                typeof(ArchitectureGuidelinesTests).Assembly.GetTypes().Select(t => t.Name));

            var missing = GetProductionTypes()
                .Where(t => t.IsClass && !t.IsNested)
                .Where(IsInScope)
                .Where(t => !TestCoverageExemptions.IsExempt(t))
                .Where(t => !TestCoverageExemptions.IsGrandfathered(t)) // pre-0.5.0 debt (backlog P-O)
                .Where(t => !testTypeNames.Contains(t.Name + "Tests"))
                .Select(t => t.FullName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            Assert.True(
                missing.Count == 0,
                "Production classes without a <Name>Tests class:" + Environment.NewLine
                    + "  " + string.Join(Environment.NewLine + "  ", missing));
        }

        /// <summary>
        /// Scenario 10: KeeRadar.Shared.* must never depend on KPPasskeyChecker.* — layering
        /// hardening enforced at test time.
        /// </summary>
        [Fact]
        public void Shared_must_not_depend_on_plugin_code()
        {
            IArchRule rule = Types()
                .That()
                .ResideInNamespaceMatching("^KeeRadar\\.Shared")
                .Should()
                .NotDependOnAny(Types().That().ResideInNamespaceMatching("^KPPasskeyChecker"))
                .Because("Shared is plugin-agnostic infrastructure; it must not depend on plugin code.");

            rule.Check(Architecture);
        }

        private static bool IsInScope(Type t)
        {
            string ns = t.Namespace ?? string.Empty;
            return ns == "KPPasskeyChecker" || ns.StartsWith("KPPasskeyChecker.", StringComparison.Ordinal)
                || ns == "KeeRadar.Shared" || ns.StartsWith("KeeRadar.Shared.", StringComparison.Ordinal);
        }

        // GetTypes() can throw if a KeePass-derived type fails to load; fall back to the types
        // that did load so the guard still runs (and never silently swallows a real gap).
        private static IEnumerable<Type> GetProductionTypes()
        {
            try
            {
                return ProductionAssembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }
    }
}
