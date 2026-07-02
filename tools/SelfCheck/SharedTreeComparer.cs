using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

// Deliberately in the GLOBAL namespace (no "namespace" block): this type must be reachable
// without a "using" from two independent call sites that live in different namespaces —
// KPPasskeyChecker.SelfCheck.SelfCheck (the csc /langversion:5 harness, tools\SelfCheck\SelfCheck.cs)
// and KPPasskeyChecker.Tests.Architecture.SharedTreeDriftGuardTests (the xUnit regression pin,
// which references "SharedTreeComparer" unqualified and carries no matching "using"). Putting it
// in either namespace would require editing the other call site; the global namespace keeps both
// call sites — and this file — untouched by that choice.
//
/// <summary>
/// Pure, dependency-free (BCL-only: System.IO + System.Security.Cryptography) recursive
/// directory-tree comparer. Compiles unchanged under csc /langversion:5 (the SDK-free
/// tools\SelfCheck harness, the release-blocking Shared-drift gate) AND under the modern
/// SDK/xUnit test project (KPPasskeyChecker.Tests\Architecture\SharedTreeDriftGuardTests.cs,
/// which pins the comparison/detection algorithm against a synthetic fixture) — one algorithm,
/// two call sites. This type intentionally lives outside src\Shared and src\KPPasskeyChecker so
/// it is included by neither build.ps1's flat-folder staging nor the shipped .plgx/.dll, and
/// outside the KPPasskeyChecker.* / KeeRadar.Shared.* namespaces so the xUnit needs-tests guard
/// (ArchitectureGuidelinesTests.IsInScope) never requires a production-class test pairing for it
/// — it is test/tooling infrastructure, not shipped runtime surface.
///
/// Architecture-Assessment 2026-07-02, Achse 1 / Rangliste #1 ("Shared-Drift-Guard").
/// </summary>
internal static class SharedTreeComparer
{
    /// <summary>
    /// Compares every file reachable (recursively) under <paramref name="treeA"/> and
    /// <paramref name="treeB"/> and returns the relative paths (forward-slash-normalized is NOT
    /// applied here — callers get the OS-native separator, matching Path.Combine's convention)
    /// of every file that is missing on either side or whose content differs (compared via
    /// SHA-256 digest). An empty result means the two trees are byte-identical.
    /// </summary>
    public static IReadOnlyList<string> FindDivergentFiles(string treeA, string treeB)
    {
        if (treeA == null) throw new ArgumentNullException("treeA");
        if (treeB == null) throw new ArgumentNullException("treeB");

        var relativePathsA = GetRelativeFilePaths(treeA);
        var relativePathsB = GetRelativeFilePaths(treeB);

        var allRelativePaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string p in relativePathsA.Keys) allRelativePaths.Add(p);
        foreach (string p in relativePathsB.Keys) allRelativePaths.Add(p);

        var divergent = new List<string>();
        foreach (string relativePath in allRelativePaths)
        {
            string pathA;
            string pathB;
            bool inA = relativePathsA.TryGetValue(relativePath, out pathA);
            bool inB = relativePathsB.TryGetValue(relativePath, out pathB);

            if (!inA || !inB)
            {
                divergent.Add(relativePath);
                continue;
            }

            if (!FileHashesEqual(pathA, pathB))
                divergent.Add(relativePath);
        }

        return divergent;
    }

    // Maps relativePath (OS-native separators, case-preserving key comparer supplied by the
    // caller via the dictionary) -> full path, for every file under root (recursive).
    private static Dictionary<string, string> GetRelativeFilePaths(string root)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(root))
            return map;

        string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        foreach (string fullPath in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            string relative = fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(prefix.Length)
                : fullPath;
            map[relative] = fullPath;
        }

        return map;
    }

    private static bool FileHashesEqual(string pathA, string pathB)
    {
        byte[] hashA = ComputeSha256(pathA);
        byte[] hashB = ComputeSha256(pathB);
        return hashA.SequenceEqual(hashB);
    }

    private static byte[] ComputeSha256(string path)
    {
        using (var sha256 = SHA256.Create())
        using (var stream = File.OpenRead(path))
        {
            return sha256.ComputeHash(stream);
        }
    }
}
