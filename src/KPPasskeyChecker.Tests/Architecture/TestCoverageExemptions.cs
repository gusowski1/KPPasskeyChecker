using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace KPPasskeyChecker.Tests.Architecture
{
    /// <summary>
    /// Explicit, documented exemptions from the "every production class needs a corresponding
    /// &lt;Class&gt;Tests class" guard (see <see cref="ArchitectureGuidelinesTests"/>).
    ///
    /// <see cref="Entries"/> only carries exemptions that cannot be expressed as a structural
    /// rule and therefore require a named type + a reason.
    ///
    /// Structural exemptions (compiler-generated, IsExternalInit, PluginVersion, AssemblyInfo,
    /// *Ext suffix, enums, delegates, WinForms Forms/UserControls) are convention rules evaluated
    /// directly in <see cref="IsExempt"/> and are NOT duplicated as list entries.
    ///
    /// Every entry MUST carry a non-empty reason and MUST reference a type that still exists —
    /// both enforced by <see cref="TestCoverageExemptionsTests"/> (Scenario 9).
    /// </summary>
    internal static class TestCoverageExemptions
    {
        public static readonly IReadOnlyList<(string TypeFullName, string Reason)> Entries =
            new List<(string TypeFullName, string Reason)>
            {
                (
                    "KPPasskeyChecker.UI.PluginIcon",
                    "Static embedded-resource icon accessor without business logic; returns a " +
                    "bundled Image/Icon only."
                ),
                (
                    "KeeRadar.Shared.KeePassUi.EntryDetailForm",
                    "WinForms UI composition (visual/event-driven); not meaningfully testable in " +
                    "isolation without a UI harness."
                ),
                (
                    "KeeRadar.Shared.KeePassUi.EntryDetailRow",
                    "WinForms-adjacent row/view base type without isolable business logic."
                ),
                (
                    "KPPasskeyChecker.Data.PasskeyApiClient",
                    "HTTP transport over the shared static HttpClient; unit-testing would require " +
                    "an HTTP-mocking production seam, which the codebase avoids by design. " +
                    "Security-critical behaviour (PGP fail-closed verification and the 16 MB " +
                    "decompression-bomb guard) is covered end-to-end by the committed .sig " +
                    "fixture in the SelfCheck harness."
                ),
                (
                    "KeeRadar.Shared.Http.ConditionalHttpFetcher",
                    "Conditional HTTP fetch (If-None-Match/ETag) over the shared static " +
                    "HttpClient; same rationale as the API client — mocking would force a " +
                    "production seam, and the security-critical path is covered end-to-end by " +
                    "the committed .sig fixture."
                ),
                (
                    "KPPasskeyChecker.Data.PasskeyDirectoryService",
                    "Fetch-and-cache lifecycle orchestrator; hard-wires the HTTP API client and " +
                    "the file-system cache in a private constructor with no injection seam, and " +
                    "initialisation starts a live background fetch and timer. Unit-testing its " +
                    "refresh/lifecycle would require a production dependency-injection seam and " +
                    "pull in the exempted HTTP transport; its own logic is thin property-mapping, " +
                    "and the fetch/verify path is covered by the committed .sig fixture."
                ),
            };

        /// <summary>
        /// Grandfathered baseline: production classes that already existed before v0.5.0 and have
        /// no unit tests yet. They are accepted as TECHNICAL DEBT — the needs-tests guard does not
        /// require tests for them (yet).
        ///
        /// This baseline is a RATCHET: it may only shrink. Per the "touch it -&gt; test it" rule,
        /// when one of these files is next modified, add its &lt;Class&gt;Tests and REMOVE the
        /// entry here — <see cref="TestCoverageExemptionsTests"/> fails if a grandfathered class
        /// already has a test (graduated) or no longer exists (stale).
        ///
        /// Currently empty: the full test-coverage backlog is complete — DomainCandidateGenerator
        /// and PublicSuffixList graduated to real tests (Shared\DomainMatching\
        /// DomainCandidateGeneratorTests.cs / PublicSuffixListTests.cs), and the remaining HTTP
        /// transport / lifecycle-orchestrator classes moved to the documented, reasoned
        /// exemptions in <see cref="Entries"/> above rather than staying silently grandfathered.
        /// </summary>
        public static readonly IReadOnlyList<string> Grandfathered = new List<string>
        {
        };

        /// <summary>True if <paramref name="t"/> is a grandfathered pre-0.5.0 technical-debt class.</summary>
        public static bool IsGrandfathered(Type t) => Grandfathered.Contains(t.FullName);

        /// <summary>
        /// True if <paramref name="t"/> is exempt from the needs-tests guard — either via a
        /// structural convention rule or via an explicit <see cref="Entries"/> listing.
        /// </summary>
        public static bool IsExempt(Type t)
        {
            // Compiler-generated types, closures, &lt;PrivateImplementationDetails&gt;.
            if (t.IsDefined(typeof(CompilerGeneratedAttribute), false) || t.Name.IndexOf('<') >= 0)
            {
                return true;
            }

            // Polyfill helper: System.Runtime.CompilerServices.IsExternalInit
            if (t.Namespace == "System.Runtime.CompilerServices")
            {
                return true;
            }

            // Version/meta constants: exact class names PluginVersion / AssemblyInfo.
            if (t.Name == "PluginVersion" || t.Name == "AssemblyInfo")
            {
                return true;
            }

            // KeePass entry points: class name ends with "Ext".
            if (t.Name.EndsWith("Ext", StringComparison.Ordinal))
            {
                return true;
            }

            // Enums and delegates carry no behaviour to unit-test.
            if (t.IsEnum || typeof(Delegate).IsAssignableFrom(t))
            {
                return true;
            }

            // WinForms UI (Forms / user controls): visual/event-driven, not unit-testable in
            // isolation without a UI harness (covers e.g. PasskeySettingsForm, EntryDetailForm).
            if (typeof(Form).IsAssignableFrom(t) || typeof(UserControl).IsAssignableFrom(t))
            {
                return true;
            }

            // Explicit, documented exemptions (see Entries above).
            if (Entries.Any(e => e.TypeFullName == t.FullName))
            {
                return true;
            }

            return false;
        }
    }
}
