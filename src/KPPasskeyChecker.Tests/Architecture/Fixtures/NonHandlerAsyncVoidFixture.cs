using System.Threading.Tasks;

namespace KPPasskeyChecker.Tests.Architecture.Fixtures
{
    /// <summary>
    /// RED-NACHWEIS-FIXTURE (Story P-S, Guard 2: Nicht-Handler-<c>async void</c> verbieten).
    ///
    /// Diese Klasse liegt AUSSCHLIESSLICH im Testprojekt fuer
    /// <see cref="ArchitectureHardeningGuidelinesTests.Guard2_async_void_test_catches_non_handler_violation"/>
    /// und wird NIE in KPPasskeyChecker.dll/.plgx geshippt.
    ///
    /// Simuliert eine fire-and-forget-<c>async void</c>-Methode AUSSERHALB einer echten
    /// WinForms-Event-Handler-Signatur (die whitelisted 2-Parameter-Form
    /// "(object sender, EventArgs e)" fehlt hier absichtlich — nur ein Parameter, kein
    /// EventArgs-Typ).
    ///
    /// WICHTIG fuer den coder (GREEN-Schritt): Guard 2 arbeitet laut Story-Skizze via
    /// <c>ProductionAssembly.GetTypes()</c> + <c>AsyncStateMachineAttribute</c>-Reflection. Damit
    /// diese Fixture erfasst wird, muss der Reflection-Scan (wie bei Guard 1) zusaetzlich die
    /// Testassembly selbst durchsuchen (z.B. eine Liste von Assemblies statt nur
    /// ProductionAssembly), gefiltert auf einen klar erkennbaren Fixture-Marker-Namespace/-Attribut,
    /// damit im Produktivbetrieb (nur ProductionAssembly) nichts davon auftaucht.
    /// </summary>
    internal sealed class RogueFireAndForgetType
    {
        // Verletzung: async void, aber KEINE (object sender, EventArgs e)-Signatur -> darf nicht
        // von der IsWinFormsEventHandler-Whitelist erfasst werden. Ruft await auf, damit der
        // Compiler tatsaechlich eine Async-State-Machine erzeugt (AsyncStateMachineAttribute) statt
        // eines synchronen "async void" ohne await, was der Guard sonst evtl. nicht als "real async"
        // erkennen wuerde.
        private async void FireAndForgetSingleParameter(object onlyOneParameter)
        {
            await Task.Delay(1).ConfigureAwait(false);
        }

        // Zweite Verletzung, ganz ohne Parameter — deckt den Fall ab, dass ein
        // IsWinFormsEventHandler-Check evtl. nur auf Parameteranzahl != 2 statt auf Typen prueft.
        private async void FireAndForgetNoParameters()
        {
            await Task.Delay(1).ConfigureAwait(false);
        }
    }
}
