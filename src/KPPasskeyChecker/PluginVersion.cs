namespace KPPasskeyChecker
{
    internal static class PluginVersion
    {
        public const string Current = "0.1.0";
        public const string RepoUrl = "https://github.com/gusowski1/KPPasskeyChecker";

        // KeePass downloads this file and compares "KPPasskeyChecker:<version>" (the
        // AssemblyTitle and AssemblyFileVersion) to decide whether a newer version exists.
        // The file lives at the repo root on the default branch; bump its version line on
        // every release. See https://keepass.info/help/v2_dev/plg_index.html#upd
        public const string UpdateUrl = "https://raw.githubusercontent.com/gusowski1/KPPasskeyChecker/main/VersionInfo.txt";
    }
}
