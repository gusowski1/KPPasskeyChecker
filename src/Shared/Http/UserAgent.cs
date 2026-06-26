// Shared KeeRadar infrastructure — canonical source: KPPasskeyChecker/src/Shared
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
