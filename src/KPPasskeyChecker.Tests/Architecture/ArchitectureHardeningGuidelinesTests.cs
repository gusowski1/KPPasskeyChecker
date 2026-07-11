using System;
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
    /// These tests define four additive architecture-hardening guards (layering, non-handler
    /// async void, naming conventions, UI/PGP isolation) together with
    /// <see cref="ArchitectureHardeningGuidelines"/> (the production-side guard helper class this
    /// file consumes).
    ///
    /// API note (verified against the REAL installed TngTech.ArchUnitNET 0.13.3 /
    /// TngTech.ArchUnitNET.xUnit 0.13.3 assemblies via a throwaway reflection probe, not against
    /// docs\archunitnet-reference.md alone, which does not enumerate every exception type):
    ///   - `IArchRule.Check(Architecture)` throws <see cref="ArchUnitNET.xUnit.FailedArchRuleException"/>
    ///     on violation (assembly `ArchUnitNET.xUnit`) — there is NO
    ///     `ArchUnitNET.Fluent.Exceptions.ArchitectureViolationException` in this package version
    ///     (that type does not exist at all in 0.13.3; an earlier draft of this file incorrectly
    ///     assumed it and has been corrected).
    ///   - `Types().That()` returns `GivenTypesThat` (namespace
    ///     `ArchUnitNET.Fluent.Syntax.Elements.Types`), which exposes `ResideInNamespaceMatching`
    ///     returning `GivenTypesConjunction`, which exposes `.Or()`/`.And()` returning back to
    ///     `GivenTypesThat` — so `.ResideInNamespaceMatching(a).Or().ResideInNamespaceMatching(b)`
    ///     compiles as expected (confirmed).
    ///   - `Interfaces(bool includeReferenced = false)` takes a bool parameter (not parameterless).
    ///   - `TypesShould.NotDependOnAny(IObjectProvider&lt;IType&gt; types)` accepts the result of
    ///     `Types().That()...` directly (confirmed overload exists).
    ///
    /// IMPORTANT — internal consistency of the two Architecture-derived scenarios per Fluent guard
    /// (Guard 1 / Guard 3a): a SINGLE `IArchRule` evaluated against a SINGLE `Architecture` cannot
    /// simultaneously be "green against real code" and "throws against the fixture" — if the
    /// fixture is loaded into the Architecture, the rule always throws (the fixture always
    /// violates it); if it is not loaded, the rule can never demonstrate catching it. This file
    /// therefore requires the coder to build TWO Architecture instances:
    ///   - `ArchitectureHardeningGuidelines.ProductionOnlyArchitecture` — loaded from ONLY the
    ///     production assembly (typeof(KPPasskeyChecker.KPPasskeyCheckerExt).Assembly). Used for
    ///     the "green against real code" assertions (Szenarien 1, 6) so the fixtures (which live
    ///     in the test assembly) can never be part of it.
    ///   - `ArchitectureHardeningGuidelines.ProductionAndTestArchitecture` — loaded from BOTH the
    ///     production assembly AND the test assembly (typeof(ArchitectureHardeningGuidelinesTests)
    ///     .Assembly). Used exclusively for the ROT-proof assertions (Szenarien 2, 7) so the
    ///     fixtures are visible and the rule can demonstrably catch them.
    /// The two rule OBJECTS (`DataMustNotDependOnUiRule`, `InterfacesStartWithIRule`) are the same
    /// IArchRule instances in both cases — only the Architecture passed to `.Check(...)` differs.
    /// This mirrors exactly how a real ArchUnitNET user would keep a clean acceptance test (only
    /// production code) separate from a fixture-based regression proof (test doubles included) —
    /// it is not a workaround, it is the correct modelling of "this rule enforces X against
    /// shipped code" plus "this rule provably catches X if it ever recurs".
    ///
    /// Guard 2 (async void) and Guard 3b (Form suffix) do NOT have this problem, because they are
    /// list-returning reflection scans (`FindNonHandlerAsyncVoidMethods()` /
    /// `FindRealFormsWithoutFormSuffix()`), not throwing `IArchRule.Check()` calls — a single scan
    /// across BOTH assemblies naturally yields a list that both (a) excludes the two real
    /// production handlers / two real production Forms (asserted via `Assert.DoesNotContain`) and
    /// (b) includes the fixture offenders (asserted via `Assert.Contains`) at the same time. No
    /// second assembly-set is needed for these two guards.
    ///
    /// Guard/helper surface — see also the XML doc on each fixture in
    /// Architecture\Fixtures\*.cs for the exact expectations:
    ///
    /// 1. A production-side helper class `ArchitectureHardeningGuidelines` (this test file's
    ///    `using` surface expects it to expose the below static members). Location: a dedicated
    ///    file `Architecture\ArchitectureHardeningGuidelines.cs` alongside the pre-existing
    ///    `ArchitectureGuidelines.cs` (kept untouched, additive only, per Immutable-Core).
    ///
    /// 2. Guard 1 (Layering Data.* &#8869; UI) — ArchUnitNET-Fluent:
    ///    <code>
    ///    IArchRule DataMustNotDependOnUiRule =
    ///        Types().That().ResideInNamespaceMatching("^KPPasskeyChecker\\.Data")
    ///            .Should().NotDependOnAny(
    ///                Types().That().ResideInNamespaceMatching("^System\\.Windows\\.Forms")
    ///                    .Or().ResideInNamespaceMatching("^KPPasskeyChecker\\.UI"))
    ///            .Because("Data layer must stay UI-agnostic.");
    ///    </code>
    ///    Evaluate this SAME rule object against `ProductionOnlyArchitecture` (Szenario 1, must
    ///    pass silently) and against `ProductionAndTestArchitecture` (Szenario 2, must throw
    ///    `FailedArchRuleException` naming `RogueDataLayerType`, from
    ///    Fixtures\DataLayerUiLeakFixture.cs, namespace KPPasskeyChecker.Data, physically inside
    ///    the TEST assembly).
    ///
    /// 3. Guard 2 (Non-handler async void) — Reflection, scanning BOTH assemblies (production +
    ///    test, so a single scan naturally satisfies both the green-real-code and the
    ///    catches-fixture assertions):
    ///    <code>
    ///    var offenders = assemblies.SelectMany(a =&gt; a.GetTypes())
    ///        .SelectMany(t =&gt; t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
    ///            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
    ///        .Where(m =&gt; m.GetCustomAttribute&lt;AsyncStateMachineAttribute&gt;() != null
    ///                 &amp;&amp; m.ReturnType == typeof(void))
    ///        .Where(m =&gt; !IsWinFormsEventHandler(m))
    ///        .Select(m =&gt; m.DeclaringType.FullName + "." + m.Name)
    ///        .ToList();
    ///    </code>
    ///    `IsWinFormsEventHandler(MethodInfo m)` = exactly 2 parameters, parameter[0].ParameterType
    ///    == typeof(object), typeof(EventArgs).IsAssignableFrom(parameter[1].ParameterType). Must
    ///    keep `KPPasskeyCheckerExt.OnSettingsMenuClick` and
    ///    `PasskeySettingsForm.OnRefreshNowClick` green (both real `(object sender, EventArgs e)`).
    ///    Must catch `Fixtures.RogueFireAndForgetType.FireAndForgetSingleParameter` (1 param) and
    ///    `...FireAndForgetNoParameters` (0 params).
    ///
    /// 4. Guard 3a (Interface I-prefix) — ArchUnitNET-Fluent:
    ///    <code>
    ///    IArchRule InterfacesStartWithIRule =
    ///        Interfaces().Should().HaveNameStartingWith("I")
    ///            .Because("Interface naming convention.");
    ///    </code>
    ///    Same dual-Architecture split as Guard 1: `ProductionOnlyArchitecture` for the
    ///    green-today assertion (Szenario 6), `ProductionAndTestArchitecture` for the ROT-proof
    ///    (Szenario 7, must throw `FailedArchRuleException` naming
    ///    `RogueInterfaceWithoutIPrefix`).
    ///
    /// 5. Guard 3b (Real Form-derivations end with "Form") — Reflection, scanning BOTH
    ///    assemblies (same single-scan reasoning as Guard 2):
    ///    <code>
    ///    var offenders = assemblies.SelectMany(a =&gt; a.GetTypes())
    ///        .Where(t =&gt; typeof(System.Windows.Forms.Form).IsAssignableFrom(t)
    ///                 &amp;&amp; !t.Name.EndsWith("Form", StringComparison.Ordinal)
    ///                 &amp;&amp; t.Name.IndexOf(".Designer", StringComparison.Ordinal) &lt; 0)
    ///        .Select(t =&gt; t.FullName).ToList();
    ///    </code>
    ///    Must keep `PasskeySettingsForm` and `KeeRadar.Shared.KeePassUi.EntryDetailForm` green.
    ///    Must catch `Fixtures.RogueDialogWithoutFormSuffix`.
    ///    Must NOT flag `PasskeyColumnProvider` / `PluginIcon` / `PasskeyDetailModelBuilder`
    ///    (none derive from Form — this is the whole point of the base-type-based rule, NOT a
    ///    "*.UI.* ends with Form" rule).
    ///
    /// Minimal static surface on `ArchitectureHardeningGuidelines` that this test file's `using`s
    /// rely on (if renamed, this test file must be updated in lockstep, per the "touch it -&gt;
    /// test it" ratchet):
    ///   - `ArchUnitNET.Domain.Architecture ArchitectureHardeningGuidelines.ProductionOnlyArchitecture`
    ///     (production assembly ONLY, built once, static readonly)
    ///   - `ArchUnitNET.Domain.Architecture ArchitectureHardeningGuidelines.ProductionAndTestArchitecture`
    ///     (production assembly + this test assembly, built once, static readonly)
    ///   - `IArchRule ArchitectureHardeningGuidelines.DataMustNotDependOnUiRule`
    ///   - `System.Text.RegularExpressions.Regex ArchitectureHardeningGuidelines.DataLayerNamespaceFilter`
    ///     (the same `^KPPasskeyChecker\.Data` pattern used inside `DataMustNotDependOnUiRule`,
    ///     exposed separately so Szenario 3 can assert the filter itself never matches
    ///     KeeRadar.Shared.KeePassUi.* without needing the rule to throw)
    ///   - `IArchRule ArchitectureHardeningGuidelines.InterfacesStartWithIRule`
    ///   - `System.Collections.Generic.IReadOnlyList&lt;string&gt; ArchitectureHardeningGuidelines.FindNonHandlerAsyncVoidMethods()`
    ///     (scans BOTH the production assembly and the test assembly)
    ///   - `System.Collections.Generic.IReadOnlyList&lt;string&gt; ArchitectureHardeningGuidelines.FindRealFormsWithoutFormSuffix()`
    ///     (scans BOTH the production assembly and the test assembly)
    ///   - `bool ArchitectureHardeningGuidelines.IsWinFormsEventHandler(System.Reflection.MethodInfo m)`
    ///
    /// Additive additive hardening guards (BCL-only shipping, empty-catch source scan, HttpClient
    /// encapsulation) are appended below the original nine scenarios; see the XML docs on their
    /// respective <see cref="ArchitectureHardeningGuidelines"/> members and on the permanent
    /// Synthetik-fixtures in Architecture\Fixtures\*.cs for the exact expectations.
    /// </summary>
    public class ArchitectureHardeningGuidelinesTests
    {
        // ---- Guard 1: Layering KPPasskeyChecker.Data.* must not depend on UI/WinForms --------

        /// <summary>
        /// Szenario 1: green today. Real production KPPasskeyChecker.Data.* has no
        /// System.Windows.Forms using and no .UI.* reference (verified by Grep) — so the rule must
        /// run clean against the PRODUCTION-ONLY architecture (fixtures excluded by construction,
        /// see class remarks on the two-Architecture split).
        /// </summary>
        [Fact]
        public void Guard1_layering_rule_is_green_against_real_production_code()
        {
            ArchitectureHardeningGuidelines.DataMustNotDependOnUiRule
                .Check(ArchitectureHardeningGuidelines.ProductionOnlyArchitecture);
        }

        /// <summary>
        /// Szenario 2 (ROT-proof): the synthetic fixture
        /// Fixtures.DataLayerUiLeakFixture.cs (namespace KPPasskeyChecker.Data, physically in the
        /// test assembly) depends on both System.Windows.Forms.MessageBox and
        /// KPPasskeyChecker.UI.PasskeyColumnProvider. Checked against the PRODUCTION+TEST
        /// architecture (see class remarks), the layering rule must catch it and the real thrown
        /// type is ArchUnitNET.xUnit.FailedArchRuleException (verified against the installed
        /// 0.13.3 assembly — NOT ArchUnitNET.Fluent.Exceptions.ArchitectureViolationException,
        /// which does not exist in this package).
        /// </summary>
        [Fact]
        public void Guard1_layering_test_catches_Data_to_UI_violation()
        {
            var ex = Assert.Throws<FailedArchRuleException>(() =>
                ArchitectureHardeningGuidelines.DataMustNotDependOnUiRule
                    .Check(ArchitectureHardeningGuidelines.ProductionAndTestArchitecture));

            Assert.Contains("RogueDataLayerType", ex.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Szenario 3: KeeRadar.Shared.KeePassUi (EntryDetailForm/EntryDetailRow, legitimate
        /// Shared WinForms) is NOT in the Guard-1 scope at all — the rule only targets
        /// KPPasskeyChecker.Data.*, never KeeRadar.Shared.*. This is a structural guarantee of the
        /// namespace filter itself, asserted directly against the exposed
        /// `DataLayerNamespaceFilter` regex (no need to run the throwing rule for this check) so a
        /// future accidental widening of the filter is caught.
        /// </summary>
        [Fact]
        public void Guard1_layering_rule_does_not_target_Shared_KeePassUi_namespace()
        {
            bool sharedKeePassUiInScope = ArchitectureHardeningGuidelines.ProductionOnlyArchitecture
                .Classes
                .Where(c => c.FullName.StartsWith("KeeRadar.Shared.KeePassUi.", StringComparison.Ordinal))
                .Any(c => ArchitectureHardeningGuidelines.DataLayerNamespaceFilter.IsMatch(c.FullName));

            Assert.False(
                sharedKeePassUiInScope,
                "Guard 1's Data-layer namespace filter must never match KeeRadar.Shared.KeePassUi.*.");
        }

        // ---- Guard 2: non-handler async void ---------------------------------------------------

        /// <summary>
        /// Szenario 4: green today. The two real async void methods
        /// (KPPasskeyCheckerExt.OnSettingsMenuClick, PasskeySettingsForm.OnRefreshNowClick) both
        /// have the real WinForms handler signature (object sender, EventArgs e) and must be
        /// whitelisted, i.e. NOT reported as offenders. Same scan (both assemblies) as Guard2's
        /// ROT-proof test below — no Architecture split needed here (see class remarks).
        /// </summary>
        [Fact]
        public void Guard2_async_void_rule_is_green_against_real_event_handlers()
        {
            var offenders = ArchitectureHardeningGuidelines.FindNonHandlerAsyncVoidMethods();

            Assert.DoesNotContain(offenders, o => o.Contains("OnSettingsMenuClick"));
            Assert.DoesNotContain(offenders, o => o.Contains("OnRefreshNowClick"));
        }

        /// <summary>
        /// Szenario 5 (ROT-proof): Fixtures.RogueFireAndForgetType has two async void methods with
        /// non-handler signatures (1 parameter, and 0 parameters respectively) — both must be
        /// reported by name.
        /// </summary>
        [Fact]
        public void Guard2_async_void_test_catches_non_handler_violation()
        {
            var offenders = ArchitectureHardeningGuidelines.FindNonHandlerAsyncVoidMethods();

            Assert.Contains(offenders, o => o.Contains("RogueFireAndForgetType.FireAndForgetSingleParameter"));
            Assert.Contains(offenders, o => o.Contains("RogueFireAndForgetType.FireAndForgetNoParameters"));
        }

        /// <summary>
        /// Documents the exact whitelist contract for IsWinFormsEventHandler so the coder's
        /// predicate is unambiguous: exactly 2 parameters, first is exactly `object`, second is
        /// EventArgs or a type derived from EventArgs.
        /// </summary>
        [Theory]
        [InlineData("OnSettingsMenuClick", true)]
        [InlineData("OnRefreshNowClick", true)]
        public void Guard2_IsWinFormsEventHandler_whitelists_real_handlers(string methodName, bool expected)
        {
            MethodInfo handler = FindRealHandlerMethodForDocumentation(methodName);
            Assert.NotNull(handler);
            Assert.Equal(expected, ArchitectureHardeningGuidelines.IsWinFormsEventHandler(handler));
        }

        private static MethodInfo FindRealHandlerMethodForDocumentation(string methodName)
        {
            Assembly productionAssembly = typeof(KPPasskeyChecker.KPPasskeyCheckerExt).Assembly;
            return productionAssembly.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                .FirstOrDefault(m => m.Name == methodName);
        }

        // ---- Guard 3a: interfaces start with "I" ------------------------------------------------

        /// <summary>
        /// Szenario 6: green today. ILocalCache (and every other production interface) already
        /// starts with "I". Checked against PRODUCTION-ONLY architecture (see class remarks).
        /// </summary>
        [Fact]
        public void Guard3a_interface_naming_rule_is_green_against_real_production_code()
        {
            ArchitectureHardeningGuidelines.InterfacesStartWithIRule
                .Check(ArchitectureHardeningGuidelines.ProductionOnlyArchitecture);
        }

        /// <summary>
        /// Szenario 7 (ROT-proof): Fixtures.RogueInterfaceWithoutIPrefix violates the naming rule
        /// and must be named in the failure. Checked against PRODUCTION+TEST architecture (see
        /// class remarks). Real thrown type verified against the installed 0.13.3 assembly:
        /// ArchUnitNET.xUnit.FailedArchRuleException.
        /// </summary>
        [Fact]
        public void Guard3a_interface_naming_test_catches_missing_I_prefix()
        {
            var ex = Assert.Throws<FailedArchRuleException>(() =>
                ArchitectureHardeningGuidelines.InterfacesStartWithIRule
                    .Check(ArchitectureHardeningGuidelines.ProductionAndTestArchitecture));

            Assert.Contains("RogueInterfaceWithoutIPrefix", ex.Message, StringComparison.Ordinal);
        }

        // ---- Guard 3b: real Form-derivations end with "Form" ------------------------------------

        /// <summary>
        /// Szenario 8: green today. PasskeySettingsForm and KeeRadar.Shared.KeePassUi.EntryDetailForm
        /// are the only real System.Windows.Forms.Form derivations and both already end with
        /// "Form".
        /// </summary>
        [Fact]
        public void Guard3b_form_suffix_rule_is_green_against_real_production_code()
        {
            var offenders = ArchitectureHardeningGuidelines.FindRealFormsWithoutFormSuffix();

            Assert.DoesNotContain(offenders, o => o.Contains("PasskeySettingsForm"));
            Assert.DoesNotContain(offenders, o => o.Contains("EntryDetailForm"));
        }

        /// <summary>
        /// Szenario 9: non-Form .UI.* classes (PasskeyColumnProvider / PluginIcon /
        /// PasskeyDetailModelBuilder) must NEVER be flagged by the Form-suffix guard — it checks
        /// the base type (System.Windows.Forms.Form), not the namespace. A namespace-based
        /// "*.UI.* ends with Form" rule was explicitly rejected because it breaks these three
        /// legitimately-named, non-Form classes.
        /// </summary>
        [Fact]
        public void Guard3b_form_suffix_rule_does_not_flag_non_Form_UI_classes()
        {
            var offenders = ArchitectureHardeningGuidelines.FindRealFormsWithoutFormSuffix();

            Assert.DoesNotContain(offenders, o => o.Contains("PasskeyColumnProvider"));
            Assert.DoesNotContain(offenders, o => o.Contains("PluginIcon"));
            Assert.DoesNotContain(offenders, o => o.Contains("PasskeyDetailModelBuilder"));
        }

        /// <summary>
        /// Szenario 7/RED-proof counterpart for 3b: Fixtures.RogueDialogWithoutFormSuffix is a
        /// real Form derivation whose name does NOT end with "Form" and must be reported.
        /// </summary>
        [Fact]
        public void Guard3b_form_suffix_test_catches_real_Form_without_suffix()
        {
            var offenders = ArchitectureHardeningGuidelines.FindRealFormsWithoutFormSuffix();

            Assert.Contains(offenders, o => o.Contains("RogueDialogWithoutFormSuffix"));
        }

        // ---- Guard 4 (N1): UI must not depend on raw PGP internals ----------------------------

        /// <summary>
        /// Fourth hardening guard: <c>ArchitectureHardeningGuidelines.UiMustNotDependOnRawPgpRule</c>.
        ///
        /// Green-today assertion: real production code has NO UI type (neither
        /// KeeRadar.Shared.KeePassUi.* nor KPPasskeyChecker.UI.*) depending on any type in
        /// KeeRadar.Shared.Pgp except the result DTO PgpVerificationResult (the only real PGP
        /// consumer is KPPasskeyChecker.Data.PasskeyApiClient). Must therefore run clean against
        /// ProductionOnlyArchitecture (fixtures excluded by construction, see class remarks on the
        /// two-Architecture split used already by Guard 1 and Guard 3a).
        ///
        /// Rule shape (ArchUnitNET-Fluent, <c>DoNotHaveName</c> verified present in the installed
        /// 0.13.3 assembly):
        /// <code>
        /// IArchRule UiMustNotDependOnRawPgpRule =
        ///     Types().That()
        ///         .ResideInNamespaceMatching("^KeeRadar\\.Shared\\.KeePassUi")
        ///         .Or().ResideInNamespaceMatching("^KPPasskeyChecker\\.UI")
        ///         .Should().NotDependOnAny(
        ///             Types().That().ResideInNamespaceMatching("^KeeRadar\\.Shared\\.Pgp")
        ///                 .And().DoNotHaveName("PgpVerificationResult"))
        ///         .Because("UI consumes only the PgpVerificationResult DTO, never the crypto internals.");
        /// </code>
        /// Both UI layers are in scope simultaneously (Shared-KeePassUi AND plugin-.UI) — a
        /// <c>.*\.KeePassUi.*</c>-only pattern would miss the plugin-.UI layer where a leak would
        /// more plausibly occur. <c>PgpVerificationResult</c> (the result DTO) is the sole
        /// exemption.
        /// </summary>
        [Fact]
        public void Guard4_ui_pgp_isolation_rule_is_green_against_real_production_code()
        {
            ArchitectureHardeningGuidelines.UiMustNotDependOnRawPgpRule
                .Check(ArchitectureHardeningGuidelines.ProductionOnlyArchitecture);
        }

        /// <summary>
        /// ROT-proof counterpart: the permanent synthetic fixture
        /// Fixtures.UiPgpLeakFixture.cs (namespace KPPasskeyChecker.UI, physically inside the TEST
        /// assembly) declares <c>RogueUiPgpConsumer</c>, which references
        /// <c>KeeRadar.Shared.Pgp.OpenPgpSignatureVerifier</c> directly (not the exempted
        /// <c>PgpVerificationResult</c> DTO). Checked against the PRODUCTION+TEST architecture
        /// (see class remarks), the guard must catch it. The real thrown type is
        /// ArchUnitNET.xUnit.FailedArchRuleException (same verified exception type as every other
        /// guard in this file — NOT ArchUnitNET.Fluent.Exceptions.ArchitectureViolationException,
        /// which does not exist in the installed 0.13.3 package).
        /// </summary>
        [Fact]
        public void Guard4_ui_pgp_isolation_test_catches_ui_to_raw_pgp_violation()
        {
            var ex = Assert.Throws<FailedArchRuleException>(() =>
                ArchitectureHardeningGuidelines.UiMustNotDependOnRawPgpRule
                    .Check(ArchitectureHardeningGuidelines.ProductionAndTestArchitecture));

            Assert.Contains("RogueUiPgpConsumer", ex.Message, StringComparison.Ordinal);
        }

        // ---- BCL-only-shipping guard: production types depend only on BCL/KeePass/self -----

        /// <summary>
        /// Green-today assertion: real production code (KPPasskeyChecker.* + KeeRadar.Shared.*)
        /// depends only on the .NET BCL, KeePass, and itself. Uses the domain-model scan
        /// (<see cref="ArchitectureHardeningGuidelines.FindForeignNamespaceDependencies"/>), not the
        /// Fluent DSL — see that method's remarks for why.
        /// </summary>
        [Fact]
        public void BclOnly_rule_is_green_against_real_production_code()
        {
            var offenders = ArchitectureHardeningGuidelines.FindForeignNamespaceDependencies(
                ArchitectureHardeningGuidelines.ProductionOnlyArchitecture);

            Assert.True(
                offenders.Count == 0,
                "Production types must depend only on the .NET BCL, KeePass, and themselves:"
                    + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", offenders));
        }

        /// <summary>
        /// ROT-proof: Fixtures.BclOnlyLeakFixture.cs (namespace KPPasskeyChecker.Data, physically in
        /// the test assembly) depends on Xunit.FactAttribute — must be caught once the architecture
        /// additionally includes the test assembly.
        /// </summary>
        [Fact]
        public void BclOnly_test_catches_foreign_namespace_violation()
        {
            var offenders = ArchitectureHardeningGuidelines.FindForeignNamespaceDependencies(
                ArchitectureHardeningGuidelines.ProductionAndTestArchitecture);

            Assert.Contains(offenders, o => o.Contains("RogueBclOnlyLeakType") && o.Contains("Xunit"));
        }

        /// <summary>
        /// BCL-only-shipping guard PLUS-assertion: the shipped plugin project file (src\KPPasskeyChecker\
        /// KPPasskeyChecker.csproj) carries no &lt;PackageReference&gt; element.
        /// </summary>
        [Fact]
        public void BclOnly_plugin_csproj_has_no_package_reference()
        {
            string csprojPath = ArchitectureHardeningGuidelines.PluginCsprojPath();
            var references = ArchitectureHardeningGuidelines.FindPackageReferencesInCsproj(csprojPath);

            Assert.True(
                references.Count == 0,
                "KPPasskeyChecker.csproj must not carry a PackageReference (BCL-only shipping): "
                    + string.Join(", ", references));
        }

        // ---- HttpClient-encapsulation guard: non-transport production types must not depend on HttpClient --------------

        /// <summary>
        /// Green-today assertion: only the transport set (PasskeyApiClient, ConditionalHttpFetcher,
        /// and — a finding surfaced while implementing this guard, see
        /// <see cref="ArchitectureHardeningGuidelines"/>'s remarks on
        /// <c>HttpTransportTypeFullNames</c> — DomainCandidateGenerator) depends on HttpClient.
        /// </summary>
        [Fact]
        public void HttpClient_rule_is_green_against_real_production_code()
        {
            var offenders = ArchitectureHardeningGuidelines.FindNonTransportHttpClientDependencies(
                ArchitectureHardeningGuidelines.ProductionOnlyArchitecture);

            Assert.True(
                offenders.Count == 0,
                "Only the transport set (PasskeyApiClient, ConditionalHttpFetcher, "
                    + "DomainCandidateGenerator) may depend on HttpClient:"
                    + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", offenders));
        }

        /// <summary>
        /// ROT-proof: Fixtures.HttpClientLeakFixture.cs (namespace KPPasskeyChecker.UI, physically in
        /// the test assembly, NOT a transport-set member) depends on HttpClient directly — must be
        /// caught once the architecture additionally includes the test assembly.
        /// </summary>
        [Fact]
        public void HttpClient_test_catches_non_transport_violation()
        {
            var offenders = ArchitectureHardeningGuidelines.FindNonTransportHttpClientDependencies(
                ArchitectureHardeningGuidelines.ProductionAndTestArchitecture);

            Assert.Contains(offenders, o => o.Contains("RogueHttpClientLeakType"));
        }

        // ---- empty-catch guard: no (non-whitelisted) empty catch blocks in production source ---------------

        /// <summary>
        /// Scans the real production source tree (src\Shared + src\KPPasskeyChecker) via
        /// <see cref="ArchitectureHardeningGuidelines.FindEmptyCatchBlocks"/>, excluding the
        /// designated BackgroundRefreshErrorSink.cs swallow by file name. This assertion surfaces
        /// (rather than hides) any real, non-whitelisted empty catch block — resolving one found
        /// this way (log/record/rethrow, or add a reasoned, named exemption) is the coder's job, not
        /// this guard's; see the QA report for the current finding.
        /// </summary>
        [Fact]
        public void EmptyCatch_guard_flags_non_whitelisted_production_occurrences()
        {
            var offenders = ArchitectureHardeningGuidelines.FindEmptyCatchBlocks(new[]
            {
                ArchitectureHardeningGuidelines.ProductionSharedSourceRoot(),
                ArchitectureHardeningGuidelines.ProductionPluginSourceRoot(),
            });

            Assert.True(
                offenders.Count == 0,
                "Empty (non-whitelisted) catch blocks found in production source:"
                    + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", offenders));
        }

        /// <summary>
        /// ROT-proof: scoped ONLY to Architecture\Fixtures\ (never the real production tree, so this
        /// scenario can never break due to real production code — "ohne gegen echten Code zu
        /// brechen"). Fixtures.EmptyCatchFixture.cs contains a permanent, deliberately empty catch
        /// block that this scan must catch.
        /// </summary>
        [Fact]
        public void EmptyCatch_guard_catches_the_permanent_fixture()
        {
            var offenders = ArchitectureHardeningGuidelines.FindEmptyCatchBlocks(new[]
            {
                ArchitectureHardeningGuidelines.FixturesSourceRoot(),
            });

            Assert.Contains(offenders, o => o.Contains("EmptyCatchFixture.cs"));
        }
    }
}
