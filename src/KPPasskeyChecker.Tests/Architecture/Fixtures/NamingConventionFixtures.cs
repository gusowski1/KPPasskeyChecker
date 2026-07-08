using System.Windows.Forms;

namespace KPPasskeyChecker.Tests.Architecture.Fixtures
{
    /// <summary>
    /// RED-NACHWEIS-FIXTURE (Guard 3a: Interface-Namen muessen mit "I" beginnen).
    ///
    /// Nur im Testprojekt; nie geshippt. Verwendet fuer
    /// <see cref="ArchitectureHardeningGuidelinesTests.Guard3a_interface_naming_test_catches_missing_I_prefix"/>.
    ///
    /// WICHTIG fuer den coder: Guard 3a ist reines ArchUnitNET-Fluent
    /// (<c>Interfaces().Should().HaveNameStartingWith("I")</c>). Wie bei Guard 1 muss die dafuer
    /// geladene Architecture die Testassembly mit einschliessen, damit dieses Interface ueberhaupt
    /// evaluiert wird.
    /// </summary>
    public interface RogueInterfaceWithoutIPrefix
    {
        void DoSomething();
    }

    /// <summary>
    /// RED-NACHWEIS-FIXTURE (Guard 3b: echte <see cref="Form"/>-Ableitungen muessen auf
    /// "Form" enden).
    ///
    /// Nur im Testprojekt; nie geshippt. Verwendet fuer
    /// <see cref="ArchitectureHardeningGuidelinesTests.Guard3b_form_suffix_test_catches_real_Form_without_suffix"/>.
    ///
    /// Eine ECHTE <see cref="System.Windows.Forms.Form"/>-Ableitung, deren Name absichtlich NICHT
    /// auf "Form" endet (Basistyp-Verletzung, nicht Namespace-Verletzung — vgl. Assessment RF1:
    /// die Regel prueft den Basistyp, nicht ob die Klasse in ".UI." liegt).
    ///
    /// WICHTIG fuer den coder: Guard 3b arbeitet ueber Reflection
    /// (<c>typeof(Form).IsAssignableFrom(t) &amp;&amp; !t.Name.EndsWith("Form")</c>), analog zu
    /// Guard 2 muss der Scan die Testassembly einschliessen, damit dieser Typ erfasst wird.
    /// </summary>
    public sealed class RogueDialogWithoutFormSuffix : Form
    {
    }
}
