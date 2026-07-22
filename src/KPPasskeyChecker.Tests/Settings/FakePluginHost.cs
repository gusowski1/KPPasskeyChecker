using KeePass.App.Configuration;
using KeePass.DataExchange;
using KeePass.Ecas;
using KeePass.Forms;
using KeePass.Plugins;
using KeePass.UI;
using KeePass.Util;
using KeePassLib;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Cryptography.PasswordGenerator;
using KeePassLib.Keys;

namespace KPPasskeyChecker.Tests.Settings
{
    /// <summary>
    /// Minimal in-memory fake of <see cref="IPluginHost"/> for settings-store tests.
    /// <see cref="IPluginHost.CustomConfig"/> is backed by a real, freshly-constructed
    /// <see cref="AceCustomConfig"/> instance — that type is a sealed, parameterless-constructible
    /// in-memory key/value store (verified via reflection: <c>GetString</c>/<c>SetString</c>/
    /// <c>GetLong</c>/<c>SetLong</c>/<c>GetBool</c>/<c>SetBool</c> have no host/file-system
    /// dependency), so no KeePass process or on-disk config is ever touched. All other
    /// <see cref="IPluginHost"/> members are untouched by <see cref="PasskeySettingsStore"/> and
    /// therefore deliberately throw if ever exercised, so a future accidental dependency on them
    /// fails loudly instead of silently returning null.
    /// </summary>
    internal sealed class FakePluginHost : IPluginHost
    {
        public AceCustomConfig CustomConfig { get; private set; }

        public FakePluginHost()
        {
            CustomConfig = new AceCustomConfig();
        }

        public MainForm MainWindow { get { throw NotSupported(); } }
        public PwDatabase Database { get { throw NotSupported(); } }
        public CommandLineArgs CommandLineArgs { get { throw NotSupported(); } }
        public CipherPool CipherPool { get { throw NotSupported(); } }
        public KeyProviderPool KeyProviderPool { get { throw NotSupported(); } }
        public KeyValidatorPool KeyValidatorPool { get { throw NotSupported(); } }
        public FileFormatPool FileFormatPool { get { throw NotSupported(); } }
        public TempFilesPool TempFilesPool { get { throw NotSupported(); } }
        public EcasPool EcasPool { get { throw NotSupported(); } }
        public EcasTriggerSystem TriggerSystem { get { throw NotSupported(); } }
        public CustomPwGeneratorPool PwGeneratorPool { get { throw NotSupported(); } }
        public ColumnProviderPool ColumnProviderPool { get { throw NotSupported(); } }

        private static System.NotSupportedException NotSupported()
        {
            return new System.NotSupportedException(
                "FakePluginHost only supports CustomConfig — PasskeySettingsStore does not use " +
                "any other IPluginHost member.");
        }
    }
}
