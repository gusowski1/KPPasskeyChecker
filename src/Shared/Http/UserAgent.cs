// Shared KeeRadar infrastructure — canonical source: KPPasskeyChecker/src/Shared. Edit only there; propagate to consumer repos via sync-shared.ps1. Do not edit synced copies.
namespace KeeRadar.Shared.Http
{
    public static class UserAgent
    {
        public static string Build(string pluginName, string version, string repoUrl)
        {
            return pluginName + "/" + version + " (+" + repoUrl + ")";
        }
    }
}
