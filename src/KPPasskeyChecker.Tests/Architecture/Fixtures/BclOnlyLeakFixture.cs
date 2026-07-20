using Xunit;

namespace KPPasskeyChecker.Tests.Architecture.Fixtures
{
    /// <summary>
    /// Permanent RED-proof fixture for the BCL-only shipping guard (production types must not
    /// depend on a foreign/NuGet namespace).
    ///
    /// It lives exclusively in the test project, backing
    /// <see cref="ArchitectureHardeningGuidelinesTests.BclOnly_test_catches_foreign_namespace_violation"/>,
    /// and is never shipped in KPPasskeyChecker.dll/.plgx (it sits in the test project, not under
    /// src\KPPasskeyChecker\ or src\Shared\).
    ///
    /// It deliberately violates the guard by referencing <c>Xunit.FactAttribute</c> — a
    /// foreign/NuGet type — from a production-looking namespace.
    ///
    /// Same pattern as Fixtures\DataLayerUiLeakFixture.cs: the type below deliberately declares
    /// itself into the production namespace "KPPasskeyChecker.Data" (it is physically part of the
    /// test project) so that the guard's production-namespace filter picks it up once the
    /// architecture is also loaded from the test assembly
    /// (ArchitectureHardeningGuidelines.ProductionAndTestArchitecture).
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
    // NOTE: this namespace block physically lives in the test-project file
    // Architecture\Fixtures\BclOnlyLeakFixture.cs (KPPasskeyChecker.Tests), NOT under
    // src\KPPasskeyChecker\Data\. It therefore never becomes part of KPPasskeyChecker.dll/.plgx
    // (see the type documentation above).
    internal sealed class RogueBclOnlyLeakType
    {
        // Violation: a direct dependency on a foreign/NuGet type (Xunit.FactAttribute) that is
        // outside System.*/Microsoft.*/KeePass*/Coverlet*/KPPasskeyChecker.*/KeeRadar.Shared.*
        // (Coverlet is allowed because the coverage collector instruments the production assembly;
        // see the BclOrSelfNamespaceFilter comment in ArchitectureHardeningGuidelines).
        public object MakeRogueXunitInstance()
        {
            return new FactAttribute();
        }
    }
}
