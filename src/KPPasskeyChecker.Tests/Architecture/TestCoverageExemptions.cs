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
    /// rule and therefore require a named type + a reason — see the Ausnahme-Mapping table in the
    /// P-N story (CLAUDE.local.md) for "PluginIcon", "EntryDetailForm", "EntryDetailRow".
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
                    "bundled Image/Icon only (see P-N Ausnahme-Mapping)."
                ),
                (
                    "KeeRadar.Shared.KeePassUi.EntryDetailForm",
                    "WinForms UI composition (visual/event-driven); not meaningfully testable in " +
                    "isolation without a UI harness (see P-N Ausnahme-Mapping)."
                ),
                (
                    "KeeRadar.Shared.KeePassUi.EntryDetailRow",
                    "WinForms-adjacent row/view base type without isolable business logic " +
                    "(see P-N Ausnahme-Mapping)."
                ),
            };

        /// <summary>
        /// Grandfathered baseline: production classes that already existed before v0.5.0 and have
        /// no unit tests yet. They are accepted as TECHNICAL DEBT — the needs-tests guard does not
        /// require tests for them (yet). Full coverage is a backlog item (Story P-O, to be
        /// estimated via /po and then implemented via /ship).
        ///
        /// This baseline is a RATCHET: it may only shrink. Per the "touch it -&gt; test it" rule,
        /// when one of these files is next modified, add its &lt;Class&gt;Tests and REMOVE the
        /// entry here — <see cref="TestCoverageExemptionsTests"/> fails if a grandfathered class
        /// already has a test (graduated) or no longer exists (stale).
        ///
        /// Architecture-hardening TDD step 2 (GREEN, 2026-07-02): DomainCandidateGenerator and
        /// PublicSuffixList graduated out of this baseline — both are touched by the Rangliste-#3
        /// PSL-HttpClient fix (static HttpClient singleton) and now have real tests
        /// (Shared\DomainMatching\DomainCandidateGeneratorTests.cs / PublicSuffixListTests.cs), per
        /// the "touch it -&gt; test it" ratchet. PasskeyDirectoryService stays grandfathered on
        /// purpose: only the new BackgroundRefreshErrorSink (a separate, directly-tested type) is
        /// covered by the Rangliste-#2 fix — the service's own lifecycle (Start/Initialize/timer)
        /// remains untested technical debt, no graduation claimed for it here.
        /// </summary>
        public static readonly IReadOnlyList<string> Grandfathered = new List<string>
        {
            // KPPasskeyChecker.* (pre-0.5.0)
            "KPPasskeyChecker.Data.ContactInfo",
            "KPPasskeyChecker.Data.PasskeyApiClient",
            "KPPasskeyChecker.Data.PasskeyDataResult",
            "KPPasskeyChecker.Data.PasskeyDirectory",
            "KPPasskeyChecker.Data.PasskeyDirectoryService",
            "KPPasskeyChecker.Data.PasskeyEndpoints",
            "KPPasskeyChecker.Data.PasskeyEntry",
            "KPPasskeyChecker.Data.PasskeyEntryMapper",
            "KPPasskeyChecker.Settings.PasskeySettingsStore",
            "KPPasskeyChecker.UI.PasskeyDetailModelBuilder",
            // KeeRadar.Shared.* (pre-0.5.0)
            "KeeRadar.Shared.Caching.CacheEntry",
            "KeeRadar.Shared.Caching.FileSystemJsonCache",
            "KeeRadar.Shared.Http.ConditionalHttpFetcher",
            "KeeRadar.Shared.Http.FetchResult",
            "KeeRadar.Shared.Http.UserAgent",
            "KeeRadar.Shared.KeePassUi.EntryDetailModel",
            "KeeRadar.Shared.KeePassUi.LinkDetailRow",
            "KeeRadar.Shared.KeePassUi.NotesDetailRow",
            "KeeRadar.Shared.KeePassUi.PluginSettingsStoreBase",
            "KeeRadar.Shared.KeePassUi.TextDetailRow",
            "KeeRadar.Shared.Pgp.OpenPgpRsaPublicKey",
            "KeeRadar.Shared.Pgp.OpenPgpSignatureVerifier",
            "KeeRadar.Shared.Pgp.PgpPacketReader",
            "KeeRadar.Shared.Pgp.PgpVerificationResult",
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
