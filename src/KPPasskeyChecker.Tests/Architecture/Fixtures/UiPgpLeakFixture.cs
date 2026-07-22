using KeeRadar.Shared.Pgp;

namespace KPPasskeyChecker.Tests.Architecture.Fixtures
{
    /// <summary>
    /// Permanent RED-proof fixture for the UI-to-raw-PGP isolation guard.
    ///
    /// It lives exclusively in the test project, backing
    /// <see cref="ArchitectureHardeningGuidelinesTests.Guard4_ui_pgp_isolation_test_catches_ui_to_raw_pgp_violation"/>,
    /// and is never shipped in KPPasskeyChecker.dll/.plgx (it sits in the test project, not under
    /// src\KPPasskeyChecker\UI\).
    ///
    /// It deliberately violates the boundary "UI (KPPasskeyChecker.UI.* and
    /// KeeRadar.Shared.KeePassUi.*) must not depend on the raw PGP internals
    /// (OpenPgpSignatureVerifier / PgpPacketReader / OpenPgpRsaPublicKey from KeeRadar.Shared.Pgp);
    /// only the result DTO PgpVerificationResult is allowed" by referencing
    /// OpenPgpSignatureVerifier from a simulated plugin UI namespace.
    ///
    /// Same pattern as Fixtures\DataLayerUiLeakFixture.cs: the type below deliberately declares
    /// itself into the production namespace "KPPasskeyChecker.UI" (it is physically part of the
    /// test project) so that the guard filter matches it without any special-case logic, once the
    /// architecture is also loaded from the test assembly
    /// (ArchitectureHardeningGuidelines.ProductionAndTestArchitecture).
    /// </summary>
    public static class UiPgpLeakFixtureMarker
    {
        // Marker constant so the fixture's purpose stays unambiguous even when the file is read in
        // isolation, without the documentation above.
        public const string Purpose =
            "Permanent RED-proof fixture: UI must not depend on raw PGP internals " +
            "(PgpVerificationResult, the result DTO, is exempt).";
    }
}

namespace KPPasskeyChecker.UI
{
    // NOTE: this namespace block physically lives in the test-project file
    // Architecture\Fixtures\UiPgpLeakFixture.cs (KPPasskeyChecker.Tests), NOT under
    // src\KPPasskeyChecker\UI\. It therefore never becomes part of KPPasskeyChecker.dll/.plgx
    // (see the type documentation above). The deliberate "KPPasskeyChecker.UI" namespace is what
    // brings this fixture into the guard's scope once the architecture is also loaded from the
    // test assembly.
    internal sealed class RogueUiPgpConsumer
    {
        // Violation: a direct dependency on a raw PGP internals type
        // (KeeRadar.Shared.Pgp.OpenPgpSignatureVerifier) from a UI type. PgpVerificationResult
        // (the result DTO) would have been allowed — this fixture deliberately uses the forbidden
        // verifier type instead of the DTO.
        // CS0649 is suppressed deliberately: the guard inspects the field's DECLARED TYPE (the
        // type dependency), not its runtime value, so the field is intentionally left unassigned.
#pragma warning disable CS0649
        public OpenPgpSignatureVerifier RogueVerifierReference;
#pragma warning restore CS0649
    }
}
