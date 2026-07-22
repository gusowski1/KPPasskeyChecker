using System;

namespace KPPasskeyChecker.Tests.Architecture.Fixtures
{
    /// <summary>
    /// Permanent RED-proof fixture for the empty-catch guard (undocumented empty catch blocks in
    /// production code).
    ///
    /// It lives exclusively in the test project under Architecture\Fixtures\ and is never shipped
    /// in KPPasskeyChecker.dll/.plgx. Unlike the other fixtures in this folder it is not picked up
    /// through reflection/ArchUnitNET but through a source-text scan: an empty catch block is an
    /// IL construct and is not visible in metadata (see
    /// <see cref="ArchitectureHardeningGuidelines.FindEmptyCatchBlocks"/>). The RED proof
    /// therefore runs against a scan pointed only at this fixtures folder — never at the real
    /// production source tree — so that it fires reliably regardless of the current state of the
    /// production code.
    ///
    /// Contains exactly one empty catch block (no code, no comment) as the violation.
    /// </summary>
    internal sealed class RogueEmptyCatchType
    {
        public void SwallowSilently()
        {
            try
            {
                ThrowMaybe();
            }
            catch
            {
            }
        }

        private static void ThrowMaybe()
        {
            throw new InvalidOperationException("Fixture exception for the empty-catch RED proof.");
        }
    }
}
