using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using Xunit;
using Type = System.Type;
using Assembly = System.Reflection.Assembly;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace KPPasskeyChecker.Tests.Architecture
{
    /// <summary>
    /// Production-side guard/helper class for Story P-S ("Haertung der ArchUnit-Guards"), GREEN
    /// step. Implements the three additive guards specified in
    /// <see cref="ArchitectureHardeningGuidelinesTests"/> (whose XML doc header is the binding
    /// contract for this class's static surface — see that file's remarks for the full rationale,
    /// including why two <see cref="ArchUnitNET.Domain.Architecture"/> instances are needed).
    ///
    /// This is purely additive test-project infrastructure: it is NOT shipped in the .plgx/.dll
    /// (build.ps1 only packages src\KPPasskeyChecker\ + src\Shared\) and does not modify the
    /// pre-existing P-N guard in ArchitectureGuidelines.cs.
    /// </summary>
    public static class ArchitectureHardeningGuidelines
    {
        private static readonly Assembly ProductionAssembly =
            typeof(KPPasskeyChecker.KPPasskeyCheckerExt).Assembly;

        private static readonly Assembly TestAssembly =
            typeof(ArchitectureHardeningGuidelinesTests).Assembly;

        /// <summary>
        /// Loaded from ONLY the production assembly. Used for the "green against real code"
        /// assertions (Szenarien 1, 6) so the fixtures (which live in the test assembly) can
        /// never be part of it.
        /// </summary>
        public static readonly ArchUnitNET.Domain.Architecture ProductionOnlyArchitecture =
            new ArchLoader().LoadAssemblies(ProductionAssembly).Build();

        /// <summary>
        /// Loaded from BOTH the production assembly and this test assembly. Used exclusively for
        /// the ROT-proof assertions (Szenarien 2, 7) so the fixtures are visible and the rule can
        /// demonstrably catch them.
        /// </summary>
        public static readonly ArchUnitNET.Domain.Architecture ProductionAndTestArchitecture =
            new ArchLoader().LoadAssemblies(ProductionAssembly, TestAssembly).Build();

        /// <summary>
        /// Guard 1 (Layering): KPPasskeyChecker.Data.* must not depend on System.Windows.Forms
        /// or KPPasskeyChecker.UI.*. KeeRadar.Shared.KeePassUi (legitimate Shared WinForms) is
        /// out of scope by construction — the filter only ever targets
        /// "^KPPasskeyChecker\.Data", never "^KeeRadar\.Shared".
        /// </summary>
        public static readonly IArchRule DataMustNotDependOnUiRule =
            Types().That().ResideInNamespaceMatching("^KPPasskeyChecker\\.Data")
                .Should().NotDependOnAny(
                    Types().That().ResideInNamespaceMatching("^System\\.Windows\\.Forms")
                        .Or().ResideInNamespaceMatching("^KPPasskeyChecker\\.UI"))
                .Because("Data layer must stay UI-agnostic.");

        /// <summary>
        /// The same namespace pattern used inside <see cref="DataMustNotDependOnUiRule"/>,
        /// exposed separately so Szenario 3 can assert the filter itself never matches
        /// KeeRadar.Shared.KeePassUi.* without needing the rule to throw.
        /// </summary>
        public static readonly Regex DataLayerNamespaceFilter =
            new Regex("^KPPasskeyChecker\\.Data", RegexOptions.Compiled);

        /// <summary>
        /// Guard 3a (Interface naming convention): every interface must start with "I".
        /// </summary>
        public static readonly IArchRule InterfacesStartWithIRule =
            Interfaces().Should().HaveNameStartingWith("I")
                .Because("Interface naming convention.");

        /// <summary>
        /// Guard 4 (N1): KeeRadar.Shared.KeePassUi.* and KPPasskeyChecker.UI.* must not depend on
        /// any type in KeeRadar.Shared.Pgp.* except the PgpVerificationResult result DTO. Both UI
        /// layers are in scope simultaneously — this is the N1 fix the assessment calls for (a
        /// KeePassUi-only pattern would miss the plugin-.UI layer where a leak would more
        /// plausibly occur).
        /// </summary>
        public static readonly IArchRule UiMustNotDependOnRawPgpRule =
            Types().That()
                .ResideInNamespaceMatching("^KeeRadar\\.Shared\\.KeePassUi")
                .Or().ResideInNamespaceMatching("^KPPasskeyChecker\\.UI")
                .Should().NotDependOnAny(
                    Types().That().ResideInNamespaceMatching("^KeeRadar\\.Shared\\.Pgp")
                        .And().DoNotHaveName("PgpVerificationResult"))
                .Because("UI consumes only the PgpVerificationResult DTO, never the crypto internals.");

        /// <summary>
        /// Guard 2 (non-handler async void): scans BOTH the production assembly and this test
        /// assembly (so real WinForms handlers stay green and fixture offenders are caught by
        /// the same single pass — see class remarks on <see cref="ArchitectureHardeningGuidelinesTests"/>).
        /// </summary>
        public static IReadOnlyList<string> FindNonHandlerAsyncVoidMethods()
        {
            return GetTypes(ProductionAssembly)
                .Concat(GetTypes(TestAssembly))
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                .Where(m => m.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>() != null
                         && m.ReturnType == typeof(void))
                .Where(m => !IsWinFormsEventHandler(m))
                .Select(m => m.DeclaringType.FullName + "." + m.Name)
                .ToList();
        }

        /// <summary>
        /// Guard 3b (real Form-derivations must end with "Form"): scans BOTH the production
        /// assembly and this test assembly. Checks the base type
        /// (System.Windows.Forms.Form), not the namespace — so non-Form .UI.* classes (e.g.
        /// PasskeyColumnProvider, PluginIcon, PasskeyDetailModelBuilder) are never flagged.
        /// </summary>
        public static IReadOnlyList<string> FindRealFormsWithoutFormSuffix()
        {
            return GetTypes(ProductionAssembly)
                .Concat(GetTypes(TestAssembly))
                .Where(t => typeof(System.Windows.Forms.Form).IsAssignableFrom(t)
                         && !t.Name.EndsWith("Form", StringComparison.Ordinal)
                         && t.Name.IndexOf(".Designer", StringComparison.Ordinal) < 0)
                .Select(t => t.FullName)
                .ToList();
        }

        /// <summary>
        /// Whitelists exactly the real WinForms event-handler signature: exactly 2 parameters,
        /// the first exactly <see cref="object"/>, the second EventArgs or a type derived from
        /// EventArgs.
        /// </summary>
        public static bool IsWinFormsEventHandler(MethodInfo m)
        {
            if (m == null)
            {
                return false;
            }

            ParameterInfo[] parameters = m.GetParameters();
            if (parameters.Length != 2)
            {
                return false;
            }

            return parameters[0].ParameterType == typeof(object)
                && typeof(EventArgs).IsAssignableFrom(parameters[1].ParameterType);
        }

        // GetTypes() can throw if a KeePass-derived type fails to load; fall back to the types
        // that did load so the guards still run (and never silently swallow a real gap). Mirrors
        // the same defensive pattern already used by ArchitectureGuidelines.cs (P-N guard).
        private static IEnumerable<Type> GetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }
    }
}
