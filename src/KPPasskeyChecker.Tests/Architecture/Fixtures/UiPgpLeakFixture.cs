using KeeRadar.Shared.Pgp;

namespace KPPasskeyChecker.Tests.Architecture.Fixtures
{
    /// <summary>
    /// RED-NACHWEIS-FIXTURE (Guard 4: UI &#8869; rohe PGP-Interna).
    ///
    /// Diese Klasse liegt AUSSCHLIESSLICH im Testprojekt fuer
    /// <see cref="ArchitectureHardeningGuidelinesTests.Guard4_ui_pgp_isolation_test_catches_ui_to_raw_pgp_violation"/>
    /// und wird NIE in KPPasskeyChecker.dll/.plgx geshippt (liegt im Testprojekt, nicht unter
    /// src\KPPasskeyChecker\UI\).
    ///
    /// Simuliert absichtlich eine Verletzung der Isolationsgrenze "UI (KPPasskeyChecker.UI.* UND
    /// KeeRadar.Shared.KeePassUi.*) darf nicht von den rohen PGP-Interna
    /// (OpenPgpSignatureVerifier / PgpPacketReader / OpenPgpRsaPublicKey aus KeeRadar.Shared.Pgp)
    /// abhaengen — nur das Ergebnis-DTO PgpVerificationResult ist erlaubt": referenziert
    /// OpenPgpSignatureVerifier direkt aus einem simulierten Plugin-UI-Namespace.
    ///
    /// WICHTIG fuer den coder (GREEN-Schritt): identisches Muster wie
    /// Fixtures\DataLayerUiLeakFixture.cs (Guard 1) — diese Fixture deklariert sich absichtlich im
    /// PRODUCTION-Namespace "KPPasskeyChecker.UI" (siehe Namespace-Deklaration unten: die Klasse
    /// liegt physisch im Testprojekt, deklariert sich aber in den Namespace
    /// "KPPasskeyChecker.UI" hinein), damit der neue Guard 4
    /// (ResideInNamespaceMatching("^KPPasskeyChecker\.UI") .Or()
    /// ResideInNamespaceMatching("^KeeRadar\.Shared\.KeePassUi")) ohne Sonderfall-Logik zuschlaegt,
    /// sobald die Architecture zusaetzlich aus der Testassembly geladen wird
    /// (ArchitectureHardeningGuidelines.ProductionAndTestArchitecture, identisches Zwei-Architecture-
    /// Muster wie bei den Guards 1 und 3a).
    /// </summary>
    public static class UiPgpLeakFixtureMarker
    {
        // Marker-Konstante, damit dieser Fixture-Zweck auch ohne den Klassenkommentar oben
        // unmissverstaendlich bleibt, falls die Datei isoliert betrachtet wird.
        public const string Purpose =
            "Permanent RED-proof fixture: UI must not depend on raw PGP internals " +
            "(PgpVerificationResult, the result DTO, is exempt).";
    }
}

namespace KPPasskeyChecker.UI
{
    // ACHTUNG: Dieser Namespace-Block liegt physisch in der Testprojekt-Datei
    // Architecture\Fixtures\UiPgpLeakFixture.cs (KPPasskeyChecker.Tests), NICHT unter
    // src\KPPasskeyChecker\UI\. Er wird daher NIE Teil von KPPasskeyChecker.dll/.plgx (siehe
    // Klassendoku oben). Die absichtliche Namespace-Wahl "KPPasskeyChecker.UI" ist noetig, damit
    // Guard 4 (ResideInNamespaceMatching("^KPPasskeyChecker\.UI")) diese Fixture ueberhaupt in
    // seinen Pruefbereich aufnimmt, sobald der coder die Architecture zusaetzlich aus der
    // Testassembly laedt (siehe UiPgpLeakFixtureMarker-Doku).
    internal sealed class RogueUiPgpConsumer
    {
        // Verletzung: direkte Abhaengigkeit von einem rohen PGP-Interna-Typ
        // (KeeRadar.Shared.Pgp.OpenPgpSignatureVerifier) aus einem UI-Typ. PgpVerificationResult
        // (das Ergebnis-DTO) waere erlaubt gewesen -- diese Fixture nutzt bewusst den verbotenen
        // Verifier-Typ, nicht das DTO.
        // CS0649 ist absichtlich unterdrueckt: der Guard prueft den DEKLARIERTEN TYP des Feldes
        // (die Typ-Abhaengigkeit), nicht seinen Laufzeitwert -- das Feld bleibt daher bewusst
        // unzugewiesen.
#pragma warning disable CS0649
        public OpenPgpSignatureVerifier RogueVerifierReference;
#pragma warning restore CS0649
    }
}
