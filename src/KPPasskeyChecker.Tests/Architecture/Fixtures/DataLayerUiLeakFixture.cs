using System.Windows.Forms;
using KPPasskeyChecker.UI;

namespace KPPasskeyChecker.Tests.Architecture.Fixtures
{
    /// <summary>
    /// RED-NACHWEIS-FIXTURE (Guard 1: Data.* &#8869; UI).
    ///
    /// Diese Klasse existiert AUSSCHLIESSLICH als Testfixture fuer
    /// <see cref="ArchitectureHardeningGuidelinesTests.Guard1_layering_test_catches_Data_to_UI_violation"/>.
    /// Sie ist KEIN Produktivcode und wird NICHT in KPPasskeyChecker.dll/.plgx geshippt (liegt im
    /// Testprojekt, nicht unter src\KPPasskeyChecker\).
    ///
    /// Simuliert absichtlich eine Verletzung der Schichtgrenze "Data.* darf nicht von
    /// System.Windows.Forms oder KPPasskeyChecker.UI.* abhaengen": referenziert MessageBox
    /// (System.Windows.Forms) UND PasskeyColumnProvider (KPPasskeyChecker.UI.*).
    ///
    /// WICHTIG fuer den coder (GREEN-Schritt): der bestehende Layering-Guard
    /// (Shared_must_not_depend_on_plugin_code) laedt die Architecture ausschliesslich aus der
    /// PRODUKTIV-Assembly (typeof(KPPasskeyChecker.KPPasskeyCheckerExt).Assembly). Diese Fixture
    /// liegt bewusst NICHT dort, sondern im Testprojekt, damit kein Testcode in den .plgx/.dll
    /// wandert (Immutable-Core / Szenario 2 AC: "Fixture wird nach dem Nachweis wieder entfernt" —
    /// hier bereits strukturell erfuellt, da sie nie Produktivcode war).
    /// Damit der neue Guard 1 diese Klasse SEHEN und fangen kann, muss die Guard-Implementierung
    /// die ArchUnitNET-Architecture zusaetzlich zur ProductionAssembly auch aus der Testassembly
    /// selbst laden (z.B. new ArchLoader().LoadAssemblies(ProductionAssembly, TestAssembly).Build()
    /// mit TestAssembly = typeof(ArchitectureHardeningGuidelinesTests).Assembly), UND der
    /// Guard-Filter ("That().ResideInNamespaceMatching(...)") muss auf den echten Namespace-String
    /// dieser Fixture-Klasse anspringen. Diese Fixture nutzt daher bewusst den PRODUCTION-Namespace
    /// "KPPasskeyChecker.Data" als literalen String-Namespace (siehe Namespace-Deklaration unten:
    /// die Klasse liegt physisch im Testprojekt, deklariert sich aber im Namespace
    /// "KPPasskeyChecker.Data" hinein), damit der reale, ungeaenderte Guard 1
    /// (ResideInNamespaceMatching("^KPPasskeyChecker\.Data")) ohne Sonderfall-Logik zuschlaegt.
    /// </summary>
    public static class DataLayerUiLeakFixtureMarker
    {
        // Marker-Konstante, damit dieser Fixture-Zweck auch ohne den Klassenkommentar
        // oben unmissverstaendlich bleibt, falls die Datei isoliert betrachtet wird.
        public const string Purpose =
            "Permanent RED-proof fixture: the Data layer must not depend on UI/WinForms.";
    }
}

namespace KPPasskeyChecker.Data
{
    // ACHTUNG: Dieser Namespace-Block liegt physisch in der Testprojekt-Datei
    // Architecture\Fixtures\DataLayerUiLeakFixture.cs (KPPasskeyChecker.Tests), NICHT unter
    // src\KPPasskeyChecker\Data\. Er wird daher NIE Teil von KPPasskeyChecker.dll/.plgx (siehe
    // Klassendoku oben). Die absichtliche Namespace-Wahl "KPPasskeyChecker.Data" ist noetig, damit
    // Guard 1 (ResideInNamespaceMatching("^KPPasskeyChecker\.Data")) diese Fixture ueberhaupt in
    // seinen Pruefbereich aufnimmt, sobald der coder die Architecture zusaetzlich aus der
    // Testassembly laedt (siehe DataLayerUiLeakFixtureMarker-Doku).
    internal sealed class RogueDataLayerType
    {
        // Verletzung A: direkte Abhaengigkeit von System.Windows.Forms.
        public void ShowRogueMessageBox()
        {
            MessageBox.Show("This must never compile-time-depend from Data.* in production.");
        }

        // Verletzung B: direkte Abhaengigkeit von KPPasskeyChecker.UI.*.
        // CS0649 ist absichtlich unterdrueckt: der Guard prueft den DEKLARIERTEN TYP des Feldes
        // (die Typ-Abhaengigkeit), nicht seinen Laufzeitwert -- das Feld bleibt daher bewusst
        // unzugewiesen.
#pragma warning disable CS0649
        public PasskeyColumnProvider RogueUiReference;
#pragma warning restore CS0649
    }
}
