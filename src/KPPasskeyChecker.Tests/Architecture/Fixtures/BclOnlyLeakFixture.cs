using Xunit;

namespace KPPasskeyChecker.Tests.Architecture.Fixtures
{
    /// <summary>
    /// RED-NACHWEIS-FIXTURE (BCL-only-shipping guard — production types must not depend on a
    /// foreign/NuGet namespace).
    ///
    /// Diese Klasse liegt AUSSCHLIESSLICH im Testprojekt fuer
    /// <see cref="ArchitectureHardeningGuidelinesTests.BclOnly_test_catches_foreign_namespace_violation"/>
    /// und wird NIE in KPPasskeyChecker.dll/.plgx geshippt (liegt im Testprojekt, nicht unter
    /// src\KPPasskeyChecker\ oder src\Shared\).
    ///
    /// Simuliert absichtlich eine Verletzung von BCL-only-shipping guard: referenziert <c>Xunit.FactAttribute</c>
    /// (ein Fremd-/NuGet-Typ) direkt aus einem produktiv-aussehenden Namespace heraus.
    ///
    /// WICHTIG: identisches Muster wie Fixtures\DataLayerUiLeakFixture.cs (Guard 1) — diese Fixture
    /// deklariert sich absichtlich im PRODUCTION-Namespace "KPPasskeyChecker.Data" (siehe
    /// Namespace-Deklaration unten: die Klasse liegt physisch im Testprojekt, deklariert sich aber
    /// in den Namespace "KPPasskeyChecker.Data" hinein), damit BCL-only-shipping guards Produktiv-Namespace-Filter
    /// diese Fixture ueberhaupt in seinen Pruefbereich aufnimmt, sobald die Architecture zusaetzlich
    /// aus der Testassembly geladen wird (ArchitectureHardeningGuidelines.ProductionAndTestArchitecture).
    /// </summary>
    public static class BclOnlyLeakFixtureMarker
    {
        public const string Purpose =
            "RED proof for BCL-only-shipping guard (production types must depend only on the .NET BCL, KeePass, "
            + "and themselves).";
    }
}

namespace KPPasskeyChecker.Data
{
    // ACHTUNG: dieser Namespace-Block liegt physisch in der Testprojekt-Datei
    // Architecture\Fixtures\BclOnlyLeakFixture.cs (KPPasskeyChecker.Tests), NICHT unter
    // src\KPPasskeyChecker\Data\. Er wird daher NIE Teil von KPPasskeyChecker.dll/.plgx (siehe
    // Klassendoku oben).
    internal sealed class RogueBclOnlyLeakType
    {
        // Verletzung: direkte Abhaengigkeit von einem Fremd-/NuGet-Typ (Xunit.FactAttribute), der
        // nicht unter System.*/Microsoft.*/KeePass*/KPPasskeyChecker.*/KeeRadar.Shared.* liegt.
        public object MakeRogueXunitInstance()
        {
            return new FactAttribute();
        }
    }
}
