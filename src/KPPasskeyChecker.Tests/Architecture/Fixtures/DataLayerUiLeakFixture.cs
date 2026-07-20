using System.Windows.Forms;
using KPPasskeyChecker.UI;

namespace KPPasskeyChecker.Tests.Architecture.Fixtures
{
    /// <summary>
    /// Permanent RED-proof fixture for the layering guard "Data.* must not depend on UI".
    ///
    /// It exists exclusively as a test fixture for
    /// <see cref="ArchitectureHardeningGuidelinesTests.Guard1_layering_test_catches_Data_to_UI_violation"/>.
    /// It is not production code and is never shipped in KPPasskeyChecker.dll/.plgx (it sits in
    /// the test project, not under src\KPPasskeyChecker\) — which is what structurally guarantees
    /// that no test code can reach the .plgx/.dll.
    ///
    /// It deliberately violates the boundary "Data.* must not depend on System.Windows.Forms or
    /// KPPasskeyChecker.UI.*" by referencing both MessageBox (System.Windows.Forms) and
    /// PasskeyColumnProvider (KPPasskeyChecker.UI.*).
    ///
    /// The type below deliberately declares itself into the production namespace
    /// "KPPasskeyChecker.Data" (it is physically part of the test project) so that the unmodified
    /// guard filter (ResideInNamespaceMatching("^KPPasskeyChecker\.Data")) matches it without any
    /// special-case logic, once the architecture is also loaded from the test assembly
    /// (ArchitectureHardeningGuidelines.ProductionAndTestArchitecture).
    /// </summary>
    public static class DataLayerUiLeakFixtureMarker
    {
        // Marker constant so the fixture's purpose stays unambiguous even when the file is read in
        // isolation, without the documentation above.
        public const string Purpose =
            "Permanent RED-proof fixture: the Data layer must not depend on UI/WinForms.";
    }
}

namespace KPPasskeyChecker.Data
{
    // NOTE: this namespace block physically lives in the test-project file
    // Architecture\Fixtures\DataLayerUiLeakFixture.cs (KPPasskeyChecker.Tests), NOT under
    // src\KPPasskeyChecker\Data\. It therefore never becomes part of KPPasskeyChecker.dll/.plgx
    // (see the type documentation above). The deliberate "KPPasskeyChecker.Data" namespace is what
    // brings this fixture into the guard's scope once the architecture is also loaded from the
    // test assembly.
    internal sealed class RogueDataLayerType
    {
        // Violation A: a direct dependency on System.Windows.Forms.
        public void ShowRogueMessageBox()
        {
            MessageBox.Show("This must never compile-time-depend from Data.* in production.");
        }

        // Violation B: a direct dependency on KPPasskeyChecker.UI.*.
        // CS0649 is suppressed deliberately: the guard inspects the field's DECLARED TYPE (the
        // type dependency), not its runtime value, so the field is intentionally left unassigned.
#pragma warning disable CS0649
        public PasskeyColumnProvider RogueUiReference;
#pragma warning restore CS0649
    }
}
