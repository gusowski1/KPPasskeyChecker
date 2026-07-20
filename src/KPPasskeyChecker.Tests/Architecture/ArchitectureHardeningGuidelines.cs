using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Dependencies;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using Xunit;
using Type = System.Type;
using Assembly = System.Reflection.Assembly;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace KPPasskeyChecker.Tests.Architecture
{
    /// <summary>
    /// Production-side guard/helper class implementing additive architecture-hardening guards.
    /// Implements the guards specified in
    /// <see cref="ArchitectureHardeningGuidelinesTests"/> (whose XML doc header is the binding
    /// contract for this class's static surface — see that file's remarks for the full rationale,
    /// including why two <see cref="ArchUnitNET.Domain.Architecture"/> instances are needed).
    ///
    /// This is purely additive test-project infrastructure: it is NOT shipped in the .plgx/.dll
    /// (build.ps1 only packages src\KPPasskeyChecker\ + src\Shared\) and does not modify the
    /// pre-existing guard in ArchitectureGuidelines.cs.
    /// </summary>
    public static class ArchitectureHardeningGuidelines
    {
        private static readonly Assembly ProductionAssembly =
            typeof(KPPasskeyChecker.KPPasskeyCheckerExt).Assembly;

        private static readonly Assembly TestAssembly =
            typeof(ArchitectureHardeningGuidelinesTests).Assembly;

        /// <summary>
        /// Loaded from ONLY the production assembly. Used for the "green against real code"
        /// assertions (Scenarios 1, 6) so the fixtures (which live in the test assembly) can
        /// never be part of it.
        /// </summary>
        public static readonly ArchUnitNET.Domain.Architecture ProductionOnlyArchitecture =
            new ArchLoader().LoadAssemblies(ProductionAssembly).Build();

        /// <summary>
        /// Loaded from BOTH the production assembly and this test assembly. Used exclusively for
        /// the RED-proof assertions (Scenarios 2, 7) so the fixtures are visible and the rule can
        /// demonstrably catch them.
        /// </summary>
        public static readonly ArchUnitNET.Domain.Architecture ProductionAndTestArchitecture =
            new ArchLoader().LoadAssemblies(ProductionAssembly, TestAssembly).Build();

        /// <summary>
        /// Guard 1 (Layering): KPPasskeyChecker.Data.* must not depend on System.Windows.Forms
        /// or KPPasskeyChecker.UI.*. KeeRadar.Shared.KeePassUi (legitimate Shared WinForms) is
        /// out of scope by construction — the filter only ever targets
        /// "^KPPasskeyChecker\.Data", never "^KeeRadar\.Shared".
        /// </summary>
        public static readonly IArchRule DataMustNotDependOnUiRule =
            Types().That().ResideInNamespaceMatching("^KPPasskeyChecker\\.Data")
                .Should().NotDependOnAny(
                    Types().That().ResideInNamespaceMatching("^System\\.Windows\\.Forms")
                        .Or().ResideInNamespaceMatching("^KPPasskeyChecker\\.UI"))
                .Because("Data layer must stay UI-agnostic.");

        /// <summary>
        /// The same namespace pattern used inside <see cref="DataMustNotDependOnUiRule"/>,
        /// exposed separately so Scenario 3 can assert the filter itself never matches
        /// KeeRadar.Shared.KeePassUi.* without needing the rule to throw.
        /// </summary>
        public static readonly Regex DataLayerNamespaceFilter =
            new Regex("^KPPasskeyChecker\\.Data", RegexOptions.Compiled);

        /// <summary>
        /// Guard 3a (Interface naming convention): every interface must start with "I".
        /// </summary>
        public static readonly IArchRule InterfacesStartWithIRule =
            Interfaces().Should().HaveNameStartingWith("I")
                .Because("Interface naming convention.");

        /// <summary>
        /// Guard 4: KeeRadar.Shared.KeePassUi.* and KPPasskeyChecker.UI.* must not depend on
        /// any type in KeeRadar.Shared.Pgp.* except the PgpVerificationResult result DTO. Both UI
        /// layers are deliberately in scope simultaneously (a KeePassUi-only pattern would miss
        /// the plugin-.UI layer where a leak would more plausibly occur).
        /// </summary>
        public static readonly IArchRule UiMustNotDependOnRawPgpRule =
            Types().That()
                .ResideInNamespaceMatching("^KeeRadar\\.Shared\\.KeePassUi")
                .Or().ResideInNamespaceMatching("^KPPasskeyChecker\\.UI")
                .Should().NotDependOnAny(
                    Types().That().ResideInNamespaceMatching("^KeeRadar\\.Shared\\.Pgp")
                        .And().DoNotHaveName("PgpVerificationResult"))
                .Because("UI consumes only the PgpVerificationResult DTO, never the crypto internals.");

        /// <summary>
        /// Guard 2 (non-handler async void): scans BOTH the production assembly and this test
        /// assembly (so real WinForms handlers stay green and fixture offenders are caught by
        /// the same single pass — see class remarks on <see cref="ArchitectureHardeningGuidelinesTests"/>).
        /// </summary>
        public static IReadOnlyList<string> FindNonHandlerAsyncVoidMethods()
        {
            return GetTypes(ProductionAssembly)
                .Concat(GetTypes(TestAssembly))
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                .Where(m => m.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>() != null
                         && m.ReturnType == typeof(void))
                .Where(m => !IsWinFormsEventHandler(m))
                .Select(m => m.DeclaringType.FullName + "." + m.Name)
                .ToList();
        }

        /// <summary>
        /// Guard 3b (real Form-derivations must end with "Form"): scans BOTH the production
        /// assembly and this test assembly. Checks the base type
        /// (System.Windows.Forms.Form), not the namespace — so non-Form .UI.* classes (e.g.
        /// PasskeyColumnProvider, PluginIcon, PasskeyDetailModelBuilder) are never flagged.
        /// </summary>
        public static IReadOnlyList<string> FindRealFormsWithoutFormSuffix()
        {
            return GetTypes(ProductionAssembly)
                .Concat(GetTypes(TestAssembly))
                .Where(t => typeof(System.Windows.Forms.Form).IsAssignableFrom(t)
                         && !t.Name.EndsWith("Form", StringComparison.Ordinal)
                         && t.Name.IndexOf(".Designer", StringComparison.Ordinal) < 0)
                .Select(t => t.FullName)
                .ToList();
        }

        /// <summary>
        /// Whitelists exactly the real WinForms event-handler signature: exactly 2 parameters,
        /// the first exactly <see cref="object"/>, the second EventArgs or a type derived from
        /// EventArgs.
        /// </summary>
        public static bool IsWinFormsEventHandler(MethodInfo m)
        {
            if (m == null)
            {
                return false;
            }

            ParameterInfo[] parameters = m.GetParameters();
            if (parameters.Length != 2)
            {
                return false;
            }

            return parameters[0].ParameterType == typeof(object)
                && typeof(EventArgs).IsAssignableFrom(parameters[1].ParameterType);
        }

        // GetTypes() can throw if a KeePass-derived type fails to load; fall back to the types
        // that did load so the guards still run (and never silently swallow a real gap). Mirrors
        // the same defensive pattern already used by ArchitectureGuidelines.cs.
        private static IEnumerable<Type> GetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }

        // ---- BCL-only-shipping guard: production types must depend only on the .NET BCL, -----------
        // ---- KeePass, and themselves ---------------------------------------------------------------

        private static readonly Regex ProductionNamespaceFilter =
            new Regex("^(KPPasskeyChecker|KeeRadar\\.Shared)(\\.|$)", RegexOptions.Compiled);

        // "KeePass" is deliberately a bare prefix (no trailing-dot/end-of-string boundary like the
        // other alternatives): the KeePass ecosystem includes both "KeePass.*" (the host app/UI
        // namespaces) AND "KeePassLib*" (KeePassLib, KeePassLib.Collections, ...) with no dot after
        // "KeePass" in the latter — a word-boundary requirement would incorrectly reject
        // KeePassLib.PwEntry etc. as "foreign". "Coverlet" is likewise allowed: running this guard
        // under `dotnet test --collect:"XPlat Code Coverage"` rewrites the production assembly to
        // inject a "Coverlet.Core.Instrumentation.Tracker.<AssemblyName>_<guid>" dependency into
        // every instrumented method — a coverage-collector instrumentation artifact (coverlet.
        // collector is itself an already-approved PrivateAssets="all" test-only package, never
        // shipped in the .plgx/.dll), not a genuine third-party production dependency.
        private static readonly Regex BclOrSelfNamespaceFilter = new Regex(
            "^(System(\\.|$)|Microsoft(\\.|$)|KeePass|Coverlet|KPPasskeyChecker(\\.|$)|KeeRadar\\.Shared(\\.|$))",
            RegexOptions.Compiled);

        /// <summary>
        /// The BCL-only-shipping guard scans the ArchUnitNET domain-model dependency graph directly rather than the
        /// Fluent <c>OnlyDependOn</c>/<c>NotDependOnAny</c> DSL. Verified via a throwaway reflection
        /// probe against the installed 0.13.3 package (analogous to the probe already referenced in
        /// <see cref="ArchitectureHardeningGuidelinesTests"/>'s class remarks): <c>Types()</c>'s
        /// default <c>includeReferenced: false</c> means a <c>Types().That()</c> predicate never
        /// matches an externally-referenced/stub type (e.g. <c>Xunit.FactAttribute</c> or
        /// <c>System.Net.Http.HttpClient</c> — anything only ever USED, never INHERITED FROM, by the
        /// loaded assemblies) unless the provider is built with <c>Types(includeReferenced: true)</c>.
        /// <c>OnlyDependOn</c> silently never flags anything under the default (confirmed empirically:
        /// it stayed green even with a fixture depending on <c>Xunit.FactAttribute</c> loaded into the
        /// architecture), and even <c>NotDependOnAny</c> needs the <c>includeReferenced: true</c>
        /// opt-in for such a target (confirmed empirically for <c>System.Net.Http.HttpClient</c> —
        /// <c>System.Windows.Forms.Form</c> works with the Fluent default only because real production
        /// code INHERITS from it, which pulls the WinForms assembly in more fully than a type that is
        /// merely instantiated/used). This direct domain-model scan sidesteps that pitfall entirely
        /// and is reused by the HttpClient-encapsulation guard below.
        ///
        /// Flags every dependency of a production type (KPPasskeyChecker.* or KeeRadar.Shared.*)
        /// whose target does not reside in an allowed namespace (.NET BCL System.*/Microsoft.*,
        /// KeePass*, or the production namespaces themselves — i.e. self-dependencies are fine).
        /// Compiler-generated types are excluded on both sides to avoid noise from synthesized
        /// infrastructure (closures, iterator state machines, etc.).
        /// </summary>
        public static IReadOnlyList<string> FindForeignNamespaceDependencies(ArchUnitNET.Domain.Architecture architecture)
        {
            var offenders = new List<string>();
            foreach (Class c in architecture.Classes)
            {
                if (!ProductionNamespaceFilter.IsMatch(c.FullName) || c.IsCompilerGenerated)
                    continue;

                foreach (ITypeDependency dependency in c.Dependencies)
                {
                    IType target = dependency.Target;
                    if (target.IsCompilerGenerated)
                        continue;

                    string targetNamespace = target.Namespace != null ? target.Namespace.FullName : string.Empty;
                    if (BclOrSelfNamespaceFilter.IsMatch(targetNamespace))
                        continue;

                    offenders.Add(c.FullName + " -> " + target.FullName);
                }
            }
            return offenders.Distinct().OrderBy(o => o, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// BCL-only-shipping guard PLUS-assertion: parses the shipped plugin project file and returns every
        /// &lt;PackageReference&gt; element found (its Include attribute, falling back to the raw
        /// element text). An empty result means the project carries no NuGet dependency at all — the
        /// production code that <c>build.ps1</c> packages into the .plgx/.dll is BCL-only by
        /// construction, not just by the dependency-graph scan above.
        /// </summary>
        public static IReadOnlyList<string> FindPackageReferencesInCsproj(string csprojPath)
        {
            XDocument doc = XDocument.Load(csprojPath);
            return doc.Descendants("PackageReference")
                .Select(e => (string)e.Attribute("Include") ?? e.ToString())
                .ToList();
        }

        // ---- HttpClient-encapsulation guard: non-transport production types must not depend on HttpClient ---

        // The transport set: production types that legitimately own an HttpClient. Discovered during
        // implementation (throwaway probe against the real production DLL) that this is THREE
        // classes, not the two originally scoped: KeeRadar.Shared.DomainMatching.
        // DomainCandidateGenerator also owns its own static HttpClient (see its
        // InitializeAsync/LoadPslAsync) to fetch the Public Suffix List, independently of
        // PasskeyApiClient/ConditionalHttpFetcher's directory-JSON fetch path — reported as a finding
        // (transport set is 3 members, not 2) alongside this guard rather than silently narrowed.
        private static readonly HashSet<string> HttpTransportTypeFullNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "KPPasskeyChecker.Data.PasskeyApiClient",
            "KeeRadar.Shared.Http.ConditionalHttpFetcher",
            "KeeRadar.Shared.DomainMatching.DomainCandidateGenerator",
        };

        /// <summary>
        /// The HttpClient-encapsulation guard returns every production type outside <see cref="HttpTransportTypeFullNames"/>
        /// that depends on <c>System.Net.Http.HttpClient</c>. Same domain-model-scan approach as
        /// the BCL-only-shipping guard above (see its remarks) rather than the Fluent DSL, for the same
        /// <c>includeReferenced</c> reason.
        /// </summary>
        public static IReadOnlyList<string> FindNonTransportHttpClientDependencies(ArchUnitNET.Domain.Architecture architecture)
        {
            var offenders = new List<string>();
            foreach (Class c in architecture.Classes)
            {
                if (!ProductionNamespaceFilter.IsMatch(c.FullName) || c.IsCompilerGenerated)
                    continue;
                if (HttpTransportTypeFullNames.Contains(c.FullName))
                    continue;

                if (c.Dependencies.Any(d => d.Target.FullName == "System.Net.Http.HttpClient"))
                    offenders.Add(c.FullName);
            }
            return offenders.Distinct().OrderBy(o => o, StringComparer.Ordinal).ToList();
        }

        // ---- Empty-catch guard: no undocumented empty catch blocks in production source -------------

        // Empty catch blocks are invisible to System.Reflection (an IL-level construct, not
        // metadata), so this guard is a source-TEXT scan rather than a reflection/ArchUnit scan.
        // A documented (commented) or otherwise non-empty catch body is an accepted, reviewed
        // swallow; only a body that is whitespace-only — no code, no comment — is a violation. The
        // body-capturing group assumes no braces nested inside the catch body itself (true for both
        // a whitespace-only body and a comment-only body, the only two shapes this guard needs to
        // tell apart) — a catch with real nested-brace code simply never satisfies the
        // whitespace-only check below, which is the only outcome this guard relies on.
        private static readonly Regex CatchBlockPattern = new Regex(
            @"catch\s*(\([^)]*\))?\s*\{([^{}]*)\}", RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// Scans every .cs file under each of <paramref name="roots"/> (recursive) for a
        /// <c>catch</c> block whose body — once string/char literals are blanked and comments are
        /// masked (see <see cref="MaskCommentsAndStripLiterals"/>) — contains nothing but
        /// whitespace: no code, no explanatory comment. Skips <c>BackgroundRefreshErrorSink.cs</c>
        /// (its designated, reviewed fire-and-forget swallow — see that class's remarks) by file
        /// name; every other whitespace-only catch body found is returned as "path:line".
        /// </summary>
        public static IReadOnlyList<string> FindEmptyCatchBlocks(IEnumerable<string> roots)
        {
            var offenders = new List<string>();
            foreach (string root in roots)
            {
                if (!Directory.Exists(root))
                    continue;

                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    if (string.Equals(Path.GetFileName(file), "BackgroundRefreshErrorSink.cs", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string source = File.ReadAllText(file);
                    string masked = MaskCommentsAndStripLiterals(source);

                    foreach (Match m in CatchBlockPattern.Matches(masked))
                    {
                        if (m.Groups[2].Value.Trim().Length == 0)
                            offenders.Add(file + ":" + LineNumberAt(source, m.Index));
                    }
                }
            }
            return offenders;
        }

        // Best-effort C# comment/string/char-literal normalizer: replaces every character inside a
        // "//" line comment or a "/* */" block comment with a non-whitespace marker ('#', preserving
        // newlines so line numbers computed against the ORIGINAL source stay correct and so a
        // comment can never be mistaken for a whitespace-only catch body — see FindEmptyCatchBlocks
        // remarks), while every character inside an @"..." verbatim string (with "" escapes), a
        // regular "..." string (with \-escapes) or a '...' char literal is replaced with a space
        // instead (a string/char literal is code, not documentation, and its content — including any
        // "{", "}" or "'" it contains — must never leak into the brace/body matching below). Both
        // substitutions consume the whole construct in one pass, so a quote or apostrophe embedded in
        // a comment (e.g. "doesn't") is never separately misread as a string/char-literal delimiter.
        private static string MaskCommentsAndStripLiterals(string source)
        {
            var sb = new StringBuilder(source.Length);
            int i = 0;
            int n = source.Length;
            while (i < n)
            {
                char c = source[i];
                char next = i + 1 < n ? source[i + 1] : '\0';

                if (c == '/' && next == '/')
                {
                    while (i < n && source[i] != '\n') { sb.Append('#'); i++; }
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    sb.Append("##");
                    i += 2;
                    while (i < n && !(source[i] == '*' && i + 1 < n && source[i + 1] == '/'))
                    {
                        sb.Append(source[i] == '\n' ? '\n' : '#');
                        i++;
                    }
                    if (i < n) { sb.Append("##"); i += 2; }
                    continue;
                }

                if (c == '@' && next == '"')
                {
                    sb.Append("  ");
                    i += 2;
                    while (i < n)
                    {
                        if (source[i] == '"' && i + 1 < n && source[i + 1] == '"')
                        {
                            sb.Append("  ");
                            i += 2;
                            continue;
                        }
                        if (source[i] == '"') { sb.Append(' '); i++; break; }
                        sb.Append(source[i] == '\n' ? '\n' : ' ');
                        i++;
                    }
                    continue;
                }

                if (c == '"')
                {
                    sb.Append(' ');
                    i++;
                    while (i < n && source[i] != '"')
                    {
                        if (source[i] == '\\' && i + 1 < n) { sb.Append("  "); i += 2; continue; }
                        sb.Append(source[i] == '\n' ? '\n' : ' ');
                        i++;
                    }
                    if (i < n) { sb.Append(' '); i++; }
                    continue;
                }

                if (c == '\'')
                {
                    sb.Append(' ');
                    i++;
                    while (i < n && source[i] != '\'')
                    {
                        if (source[i] == '\\' && i + 1 < n) { sb.Append("  "); i += 2; continue; }
                        sb.Append(source[i] == '\n' ? '\n' : ' ');
                        i++;
                    }
                    if (i < n) { sb.Append(' '); i++; }
                    continue;
                }

                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        private static int LineNumberAt(string source, int index)
        {
            int line = 1;
            for (int idx = 0; idx < index && idx < source.Length; idx++)
                if (source[idx] == '\n') line++;
            return line;
        }

        // ---- Path resolution shared by the csproj check and the source-tree scan --------------------

        /// <summary>
        /// Climbs up from the test assembly's output directory to the repository root (the directory
        /// containing the .sln), mirroring the ascent pattern already used elsewhere in this test
        /// project (<see cref="CoverageExemptionSyncTests"/>) for locating on-disk state relative to
        /// the test binary.
        /// </summary>
        public static string LocateRepositoryRoot()
        {
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 10 && !string.IsNullOrEmpty(dir); i++)
            {
                if (Directory.GetFiles(dir, "*.sln").Length > 0)
                    return dir;

                dir = Path.GetDirectoryName(
                    dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }

            throw new InvalidOperationException(
                "Could not locate the repository root (a directory containing a .sln file) by "
                    + "walking up from " + AppContext.BaseDirectory);
        }

        public static string ProductionSharedSourceRoot()
        {
            return Path.Combine(LocateRepositoryRoot(), "src", "Shared");
        }

        public static string ProductionPluginSourceRoot()
        {
            return Path.Combine(LocateRepositoryRoot(), "src", "KPPasskeyChecker");
        }

        public static string FixturesSourceRoot()
        {
            return Path.Combine(LocateRepositoryRoot(), "src", "KPPasskeyChecker.Tests", "Architecture", "Fixtures");
        }

        public static string PluginCsprojPath()
        {
            return Path.Combine(LocateRepositoryRoot(), "src", "KPPasskeyChecker", "KPPasskeyChecker.csproj");
        }
    }
}
