using System.Windows.Forms;

namespace KPPasskeyChecker.Tests.Architecture.Fixtures
{
    /// <summary>
    /// Permanent RED-proof fixture for the interface-naming guard (interface names must start
    /// with "I").
    ///
    /// Test project only; never shipped. Backs
    /// <see cref="ArchitectureHardeningGuidelinesTests.Guard3a_interface_naming_test_catches_missing_I_prefix"/>.
    ///
    /// The guard is plain ArchUnitNET fluent syntax
    /// (<c>Interfaces().Should().HaveNameStartingWith("I")</c>), so the architecture it runs
    /// against has to include the test assembly for this interface to be evaluated at all.
    /// </summary>
    public interface RogueInterfaceWithoutIPrefix
    {
        void DoSomething();
    }

    /// <summary>
    /// Permanent RED-proof fixture for the form-naming guard (real <see cref="Form"/> derivations
    /// must end in "Form").
    ///
    /// Test project only; never shipped. Backs
    /// <see cref="ArchitectureHardeningGuidelinesTests.Guard3b_form_suffix_test_catches_real_Form_without_suffix"/>.
    ///
    /// A REAL <see cref="System.Windows.Forms.Form"/> derivation whose name deliberately does not
    /// end in "Form". This is a base-type violation, not a namespace violation: the rule checks
    /// the base type, not whether the class sits in ".UI.".
    ///
    /// The guard works through reflection
    /// (<c>typeof(Form).IsAssignableFrom(t) &amp;&amp; !t.Name.EndsWith("Form")</c>), so the scan
    /// has to include the test assembly for this type to be picked up.
    /// </summary>
    public sealed class RogueDialogWithoutFormSuffix : Form
    {
    }
}
