using KPPasskeyChecker.Data;
using KPPasskeyChecker.Settings;
using Xunit;

namespace KPPasskeyChecker.Tests.Settings
{
    /// <summary>
    /// Full public-surface coverage of <see cref="PasskeySettingsStore"/> — all three settings,
    /// defaults, round-trip, and invalid/absent value handling. Backed by
    /// <see cref="FakePluginHost"/>, an in-memory fake of
    /// <c>KeePass.Plugins.IPluginHost</c> whose <c>CustomConfig</c> is a real
    /// <c>KeePass.App.Configuration.AceCustomConfig</c> instance (no KeePass process, no disk I/O).
    /// </summary>
    public class PasskeySettingsStoreTests
    {
        // --- Scope --------------------------------------------------------------------------------

        [Fact]
        public void Scope_defaults_to_AnySupport_when_never_set()
        {
            PasskeySettingsStore store = NewStore();
            Assert.Equal(PasskeyDataScope.AnySupport, store.Scope);
        }

        [Theory]
        [InlineData(PasskeyDataScope.AnySupport)]
        [InlineData(PasskeyDataScope.PasswordlessOnly)]
        [InlineData(PasskeyDataScope.MfaOnly)]
        public void Scope_round_trips_every_enum_value(PasskeyDataScope scope)
        {
            PasskeySettingsStore store = NewStore();
            store.Scope = scope;
            Assert.Equal(scope, store.Scope);
        }

        [Fact]
        public void Scope_falls_back_to_AnySupport_when_the_stored_value_is_not_a_valid_enum_name()
        {
            FakePluginHost host = new FakePluginHost();
            host.CustomConfig.SetString("KPPasskeyChecker.Scope", "TotallyBogusValue");

            PasskeySettingsStore store = new PasskeySettingsStore(host);

            Assert.Equal(PasskeyDataScope.AnySupport, store.Scope);
        }

        [Fact]
        public void Scope_falls_back_to_AnySupport_when_the_stored_value_is_empty()
        {
            FakePluginHost host = new FakePluginHost();
            host.CustomConfig.SetString("KPPasskeyChecker.Scope", string.Empty);

            PasskeySettingsStore store = new PasskeySettingsStore(host);

            Assert.Equal(PasskeyDataScope.AnySupport, store.Scope);
        }

        [Fact]
        public void Scope_persists_via_the_documented_CustomConfig_key()
        {
            FakePluginHost host = new FakePluginHost();
            PasskeySettingsStore store = new PasskeySettingsStore(host);

            store.Scope = PasskeyDataScope.MfaOnly;

            Assert.Equal("MfaOnly", host.CustomConfig.GetString("KPPasskeyChecker.Scope", null));
        }

        // --- RefreshIntervalHours -------------------------------------------------------------------

        [Fact]
        public void RefreshIntervalHours_defaults_to_24_when_never_set()
        {
            PasskeySettingsStore store = NewStore();
            Assert.Equal(24, store.RefreshIntervalHours);
        }

        [Fact]
        public void RefreshIntervalHours_round_trips_a_positive_value()
        {
            PasskeySettingsStore store = NewStore();
            store.RefreshIntervalHours = 6;
            Assert.Equal(6, store.RefreshIntervalHours);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void RefreshIntervalHours_clamps_non_positive_values_up_to_1(int input)
        {
            PasskeySettingsStore store = NewStore();
            store.RefreshIntervalHours = input;
            Assert.Equal(1, store.RefreshIntervalHours);
        }

        [Fact]
        public void RefreshIntervalHours_allows_the_minimum_value_of_1()
        {
            PasskeySettingsStore store = NewStore();
            store.RefreshIntervalHours = 1;
            Assert.Equal(1, store.RefreshIntervalHours);
        }

        [Fact]
        public void RefreshIntervalHours_persists_via_the_documented_CustomConfig_key()
        {
            FakePluginHost host = new FakePluginHost();
            PasskeySettingsStore store = new PasskeySettingsStore(host);

            store.RefreshIntervalHours = 48;

            Assert.Equal(48L, host.CustomConfig.GetLong("KPPasskeyChecker.RefreshIntervalHours", -1));
        }

        // --- VerifyPgpSignature ---------------------------------------------------------------------

        [Fact]
        public void VerifyPgpSignature_defaults_to_true_when_never_set()
        {
            PasskeySettingsStore store = NewStore();
            Assert.True(store.VerifyPgpSignature);
        }

        [Fact]
        public void VerifyPgpSignature_round_trips_false()
        {
            PasskeySettingsStore store = NewStore();
            store.VerifyPgpSignature = false;
            Assert.False(store.VerifyPgpSignature);
        }

        [Fact]
        public void VerifyPgpSignature_round_trips_true_after_being_set_false()
        {
            PasskeySettingsStore store = NewStore();
            store.VerifyPgpSignature = false;
            store.VerifyPgpSignature = true;
            Assert.True(store.VerifyPgpSignature);
        }

        [Fact]
        public void VerifyPgpSignature_persists_via_the_documented_CustomConfig_key()
        {
            FakePluginHost host = new FakePluginHost();
            PasskeySettingsStore store = new PasskeySettingsStore(host);

            store.VerifyPgpSignature = false;

            Assert.False(host.CustomConfig.GetBool("KPPasskeyChecker.VerifyPgpSignature", true));
        }

        // --- cross-setting isolation ----------------------------------------------------------------

        [Fact]
        public void Settings_are_independent_of_each_other()
        {
            PasskeySettingsStore store = NewStore();

            store.Scope = PasskeyDataScope.PasswordlessOnly;
            store.RefreshIntervalHours = 12;
            store.VerifyPgpSignature = false;

            Assert.Equal(PasskeyDataScope.PasswordlessOnly, store.Scope);
            Assert.Equal(12, store.RefreshIntervalHours);
            Assert.False(store.VerifyPgpSignature);
        }

        // --- helpers ---------------------------------------------------------------------------------

        private static PasskeySettingsStore NewStore()
        {
            return new PasskeySettingsStore(new FakePluginHost());
        }
    }
}
