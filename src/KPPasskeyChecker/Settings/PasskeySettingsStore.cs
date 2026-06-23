using KeePass.Plugins;
using KPPasskeyChecker.Data;
using KPPasskeyChecker.Shared.KeePassUi;

namespace KPPasskeyChecker.Settings
{
    public sealed class PasskeySettingsStore : PluginSettingsStoreBase
    {
        private const string KeyScope           = "KPPasskeyChecker.Scope";
        private const string KeyRefreshInterval = "KPPasskeyChecker.RefreshIntervalHours";
        private const string KeyVerifyPgp       = "KPPasskeyChecker.VerifyPgpSignature";

        public PasskeySettingsStore(IPluginHost host) : base(host) { }

        public PasskeyDataScope Scope
        {
            get
            {
                string raw = GetString(KeyScope, "AnySupport");
                PasskeyDataScope scope;
                if (System.Enum.TryParse<PasskeyDataScope>(raw, out scope))
                    return scope;
                return PasskeyDataScope.AnySupport;
            }
            set
            {
                SetString(KeyScope, value.ToString());
            }
        }

        public int RefreshIntervalHours
        {
            get
            {
                return (int)GetLong(KeyRefreshInterval, 24);
            }
            set
            {
                SetLong(KeyRefreshInterval, value < 1 ? 1 : value);
            }
        }

        public bool VerifyPgpSignature
        {
            get
            {
                // On by default: downloaded data is verified against the pinned signing key
                // unless the user explicitly turns it off.
                return GetBool(KeyVerifyPgp, true);
            }
            set
            {
                SetBool(KeyVerifyPgp, value);
            }
        }
    }
}
