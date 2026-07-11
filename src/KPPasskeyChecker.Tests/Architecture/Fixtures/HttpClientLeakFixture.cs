using System.Net.Http;

namespace KPPasskeyChecker.Tests.Architecture.Fixtures
{
    /// <summary>
    /// RED-NACHWEIS-FIXTURE (HttpClient-encapsulation guard — Nicht-Transport-Produktivtypen
    /// duerfen nicht direkt von <see cref="System.Net.Http.HttpClient"/> abhaengen).
    ///
    /// Diese Klasse liegt AUSSCHLIESSLICH im Testprojekt fuer
    /// <see cref="ArchitectureHardeningGuidelinesTests.HttpClient_test_catches_non_transport_violation"/>
    /// und wird NIE in KPPasskeyChecker.dll/.plgx geshippt (liegt im Testprojekt, nicht unter
    /// src\KPPasskeyChecker\UI\).
    ///
    /// Simuliert absichtlich eine Verletzung von HttpClient-encapsulation guard: referenziert <c>HttpClient</c> direkt aus
    /// einem produktiv-aussehenden, NICHT zur Transport-Menge gehoerenden Namespace heraus.
    ///
    /// WICHTIG: identisches Muster wie Fixtures\UiPgpLeakFixture.cs (Guard 4) — diese Fixture
    /// deklariert sich absichtlich im PRODUCTION-Namespace "KPPasskeyChecker.UI" (siehe
    /// Namespace-Deklaration unten: die Klasse liegt physisch im Testprojekt, deklariert sich aber
    /// in den Namespace "KPPasskeyChecker.UI" hinein), damit HttpClient-encapsulation guards Produktiv-Namespace-Filter
    /// diese Fixture ueberhaupt in seinen Pruefbereich aufnimmt, sobald die Architecture zusaetzlich
    /// aus der Testassembly geladen wird (ArchitectureHardeningGuidelines.ProductionAndTestArchitecture).
    /// </summary>
    public static class HttpClientLeakFixtureMarker
    {
        public const string Purpose =
            "RED proof for HttpClient-encapsulation guard (non-transport production types must not depend on HttpClient).";
    }
}

namespace KPPasskeyChecker.UI
{
    // ACHTUNG: dieser Namespace-Block liegt physisch in der Testprojekt-Datei
    // Architecture\Fixtures\HttpClientLeakFixture.cs (KPPasskeyChecker.Tests), NICHT unter
    // src\KPPasskeyChecker\UI\. Er wird daher NIE Teil von KPPasskeyChecker.dll/.plgx (siehe
    // Klassendoku oben). "KPPasskeyChecker.UI" ist nicht Mitglied der Transport-Menge (nur
    // KPPasskeyChecker.Data.PasskeyApiClient, KeeRadar.Shared.Http.ConditionalHttpFetcher und
    // KeeRadar.Shared.DomainMatching.DomainCandidateGenerator sind es), also ist diese Verletzung
    // real fuer HttpClient-encapsulation guard.
    internal sealed class RogueHttpClientLeakType
    {
        // Verletzung: direkte Abhaengigkeit von System.Net.Http.HttpClient ausserhalb der
        // Transport-Menge.
        public object MakeRogueHttpClient()
        {
            return new HttpClient();
        }
    }
}
