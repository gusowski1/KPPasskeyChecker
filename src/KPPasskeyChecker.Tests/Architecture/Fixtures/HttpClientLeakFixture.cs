using System.Net.Http;

namespace KPPasskeyChecker.Tests.Architecture.Fixtures
{
    /// <summary>
    /// Permanent RED-proof fixture for the HttpClient-encapsulation guard (production types
    /// outside the transport set must not depend directly on
    /// <see cref="System.Net.Http.HttpClient"/>).
    ///
    /// It lives exclusively in the test project, backing
    /// <see cref="ArchitectureHardeningGuidelinesTests.HttpClient_test_catches_non_transport_violation"/>,
    /// and is never shipped in KPPasskeyChecker.dll/.plgx (it sits in the test project, not under
    /// src\KPPasskeyChecker\UI\).
    ///
    /// It deliberately violates the guard by referencing <c>HttpClient</c> from a
    /// production-looking namespace that is not part of the transport set.
    ///
    /// Same pattern as Fixtures\UiPgpLeakFixture.cs: the type below deliberately declares itself
    /// into the production namespace "KPPasskeyChecker.UI" (it is physically part of the test
    /// project) so that the guard's production-namespace filter picks it up once the architecture
    /// is also loaded from the test assembly
    /// (ArchitectureHardeningGuidelines.ProductionAndTestArchitecture).
    /// </summary>
    public static class HttpClientLeakFixtureMarker
    {
        public const string Purpose =
            "RED proof for HttpClient-encapsulation guard (non-transport production types must not depend on HttpClient).";
    }
}

namespace KPPasskeyChecker.UI
{
    // NOTE: this namespace block physically lives in the test-project file
    // Architecture\Fixtures\HttpClientLeakFixture.cs (KPPasskeyChecker.Tests), NOT under
    // src\KPPasskeyChecker\UI\. It therefore never becomes part of KPPasskeyChecker.dll/.plgx
    // (see the type documentation above). "KPPasskeyChecker.UI" is not a member of the transport
    // set — only KPPasskeyChecker.Data.PasskeyApiClient, KeeRadar.Shared.Http.ConditionalHttpFetcher
    // and KeeRadar.Shared.DomainMatching.DomainCandidateGenerator are — so this really is a
    // violation of the guard.
    internal sealed class RogueHttpClientLeakType
    {
        // Violation: a direct dependency on System.Net.Http.HttpClient from outside the transport
        // set.
        public object MakeRogueHttpClient()
        {
            return new HttpClient();
        }
    }
}
