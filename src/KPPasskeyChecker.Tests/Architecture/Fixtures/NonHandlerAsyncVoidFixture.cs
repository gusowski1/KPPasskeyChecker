using System.Threading.Tasks;

namespace KPPasskeyChecker.Tests.Architecture.Fixtures
{
    /// <summary>
    /// Permanent RED-proof fixture for the guard that forbids non-handler <c>async void</c>.
    ///
    /// It lives exclusively in the test project, backing
    /// <see cref="ArchitectureHardeningGuidelinesTests.Guard2_async_void_test_catches_non_handler_violation"/>,
    /// and is never shipped in KPPasskeyChecker.dll/.plgx.
    ///
    /// It simulates a fire-and-forget <c>async void</c> method outside a real WinForms event
    /// handler signature: the whitelisted two-parameter form "(object sender, EventArgs e)" is
    /// deliberately absent (one parameter only, no EventArgs type).
    ///
    /// The guard works through reflection over <c>AsyncStateMachineAttribute</c>, so the scan has
    /// to include the test assembly for these methods to be picked up.
    /// </summary>
    internal sealed class RogueFireAndForgetType
    {
        // Violation: async void without an (object sender, EventArgs e) signature, so it must not
        // be covered by the IsWinFormsEventHandler whitelist. It awaits so the compiler really
        // emits an async state machine (AsyncStateMachineAttribute) rather than a synchronous
        // "async void" without await, which the guard might not recognise as genuinely async.
        private async void FireAndForgetSingleParameter(object onlyOneParameter)
        {
            await Task.Delay(1).ConfigureAwait(false);
        }

        // Second violation, with no parameters at all — covers the case where an
        // IsWinFormsEventHandler check only looks at the parameter count (!= 2) instead of the
        // parameter types.
        private async void FireAndForgetNoParameters()
        {
            await Task.Delay(1).ConfigureAwait(false);
        }
    }
}
