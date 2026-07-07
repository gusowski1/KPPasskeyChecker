using KeeRadar.Shared.KeePassUi;
using KPPasskeyChecker.Tests.Settings;
using Xunit;

namespace KPPasskeyChecker.Tests.Shared.KeePassUi
{
    /// <summary>
    /// Full public-surface coverage of <see cref="PluginSettingsStoreBase"/> — all three protected
    /// Get/Set pairs, defaults, and pass-through to <c>IPluginHost.CustomConfig</c>, independent of
    /// any concrete plugin's settings store (<c>PasskeySettingsStore</c> already covers itself;
    /// this class pins the shared base's own pass-through behaviour). Exercised via a minimal
    /// test-only subclass (<see cref="TestSettingsStore"/>) that exposes the protected members,
    /// backed by <see cref="FakePluginHost"/> — the same in-memory <c>IPluginHost</c> fake used by
    /// <c>PasskeySettingsStoreTests</c>.
    /// Ownership: <c>KeeRadar.Shared.*</c> is tested exclusively in KPPasskeyChecker.Tests (the
    /// canonical source); KP2FAChecker.Tests excludes the whole namespace.
    /// </summary>
    public class PluginSettingsStoreBaseTests
    {
        // --- String -------------------------------------------------------------------------------

        [Fact]
        public void GetString_returns_the_default_when_never_set()
        {
            TestSettingsStore store = NewStore();

            Assert.Equal("default-str", store.GetStringPublic("k.str", "default-str"));
        }

        [Fact]
        public void SetString_then_GetString_round_trips_the_value()
        {
            TestSettingsStore store = NewStore();

            store.SetStringPublic("k.str", "hello");

            Assert.Equal("hello", store.GetStringPublic("k.str", "default-str"));
        }

        [Fact]
        public void SetString_writes_through_to_the_hosts_CustomConfig()
        {
            FakePluginHost host = new FakePluginHost();
            TestSettingsStore store = new TestSettingsStore(host);

            store.SetStringPublic("k.str", "hello");

            Assert.Equal("hello", host.CustomConfig.GetString("k.str", null));
        }

        // --- Long -----------------------------------------------------------------------------------

        [Fact]
        public void GetLong_returns_the_default_when_never_set()
        {
            TestSettingsStore store = NewStore();

            Assert.Equal(42L, store.GetLongPublic("k.long", 42));
        }

        [Fact]
        public void SetLong_then_GetLong_round_trips_the_value()
        {
            TestSettingsStore store = NewStore();

            store.SetLongPublic("k.long", 99);

            Assert.Equal(99L, store.GetLongPublic("k.long", 0));
        }

        [Fact]
        public void SetLong_writes_through_to_the_hosts_CustomConfig()
        {
            FakePluginHost host = new FakePluginHost();
            TestSettingsStore store = new TestSettingsStore(host);

            store.SetLongPublic("k.long", 7);

            Assert.Equal(7L, host.CustomConfig.GetLong("k.long", -1));
        }

        [Fact]
        public void SetLong_accepts_negative_values()
        {
            TestSettingsStore store = NewStore();

            store.SetLongPublic("k.long", -5);

            Assert.Equal(-5L, store.GetLongPublic("k.long", 0));
        }

        // --- Bool -----------------------------------------------------------------------------------

        [Fact]
        public void GetBool_returns_the_default_when_never_set()
        {
            TestSettingsStore store = NewStore();

            Assert.True(store.GetBoolPublic("k.bool", true));
            Assert.False(store.GetBoolPublic("k.bool2", false));
        }

        [Fact]
        public void SetBool_then_GetBool_round_trips_true()
        {
            TestSettingsStore store = NewStore();

            store.SetBoolPublic("k.bool", true);

            Assert.True(store.GetBoolPublic("k.bool", false));
        }

        [Fact]
        public void SetBool_then_GetBool_round_trips_false()
        {
            TestSettingsStore store = NewStore();

            store.SetBoolPublic("k.bool", false);

            Assert.False(store.GetBoolPublic("k.bool", true));
        }

        [Fact]
        public void SetBool_writes_through_to_the_hosts_CustomConfig()
        {
            FakePluginHost host = new FakePluginHost();
            TestSettingsStore store = new TestSettingsStore(host);

            store.SetBoolPublic("k.bool", true);

            Assert.True(host.CustomConfig.GetBool("k.bool", false));
        }

        // --- cross-type isolation -------------------------------------------------------------------

        [Fact]
        public void String_Long_and_Bool_keys_are_independent_of_each_other()
        {
            TestSettingsStore store = NewStore();

            store.SetStringPublic("shared.key", "text-value");
            store.SetLongPublic("shared.key.long", 123);
            store.SetBoolPublic("shared.key.bool", true);

            Assert.Equal("text-value", store.GetStringPublic("shared.key", ""));
            Assert.Equal(123L, store.GetLongPublic("shared.key.long", 0));
            Assert.True(store.GetBoolPublic("shared.key.bool", false));
        }

        // --- helpers -------------------------------------------------------------------------------

        private static TestSettingsStore NewStore()
        {
            return new TestSettingsStore(new FakePluginHost());
        }

        /// <summary>
        /// Minimal concrete subclass exposing <see cref="PluginSettingsStoreBase"/>'s protected
        /// Get/Set members as public pass-throughs, purely for test access — carries no settings
        /// keys or defaults of its own beyond what each test supplies inline.
        /// </summary>
        private sealed class TestSettingsStore : PluginSettingsStoreBase
        {
            public TestSettingsStore(KeePass.Plugins.IPluginHost host) : base(host) { }

            public string GetStringPublic(string key, string defaultValue) => GetString(key, defaultValue);
            public void SetStringPublic(string key, string value) => SetString(key, value);

            public long GetLongPublic(string key, long defaultValue) => GetLong(key, defaultValue);
            public void SetLongPublic(string key, long value) => SetLong(key, value);

            public bool GetBoolPublic(string key, bool defaultValue) => GetBool(key, defaultValue);
            public void SetBoolPublic(string key, bool value) => SetBool(key, value);
        }
    }
}
