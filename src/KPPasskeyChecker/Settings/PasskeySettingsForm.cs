using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using KPPasskeyChecker.Data;

namespace KPPasskeyChecker.Settings
{
    public partial class PasskeySettingsForm : Form
    {
        private readonly PasskeySettingsStore _store;

        public PasskeySettingsForm(PasskeySettingsStore store)
        {
            _store = store;
            InitializeComponent();
            LoadSettings();
            UpdateCacheStatus();
        }

        private void LoadSettings()
        {
            switch (_store.Scope)
            {
                case PasskeyDataScope.PasswordlessOnly: _rdoPasswordless.Checked = true; break;
                case PasskeyDataScope.MfaOnly:          _rdoMfa.Checked = true;          break;
                default:                                _rdoAny.Checked = true;           break;
            }

            _nudInterval.Value = _store.RefreshIntervalHours;
            _chkPgp.Checked    = _store.VerifyPgpSignature;
        }

        private void UpdateCacheStatus()
        {
            if (!PasskeyDirectoryService.IsAvailable)
            {
                _lblCacheStatus.Text = "Service not running.";
                return;
            }

            var svc = PasskeyDirectoryService.Current;

            if (svc.Directory == null)
            {
                _lblCacheStatus.Text = svc.LastError != null
                    ? "Not loaded. Error: " + svc.LastError
                    : "Loading...";
                return;
            }

            string age = svc.LastRefreshed.HasValue
                ? FormatAge(svc.LastRefreshed.Value)
                : "unknown";

            string status = svc.IsStale
                ? "Stale fallback — last fetched " + age + "\r\nError: " + svc.LastError
                : "Up to date — last fetched " + age + "\r\n" + svc.Directory.Count + " domains indexed";

            _lblCacheStatus.Text = status;
        }

        private static string FormatAge(DateTimeOffset fetchedAt)
        {
            TimeSpan age = DateTimeOffset.UtcNow - fetchedAt;
            if (age.TotalMinutes < 1) return "just now";
            if (age.TotalHours < 1)   return (int)age.TotalMinutes + " min ago";
            if (age.TotalDays < 1)    return (int)age.TotalHours + " h ago";
            return (int)age.TotalDays + " day(s) ago";
        }

        private async void OnRefreshNowClick(object sender, EventArgs e)
        {
            _btnRefreshNow.Enabled = false;
            _lblCacheStatus.Text   = "Refreshing...";

            try
            {
                await PasskeyDirectoryService.Current.RefreshAsync(true).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _lblCacheStatus.Text   = "Refresh failed: " + ex.Message;
                _btnRefreshNow.Enabled = true;
                return;
            }

            UpdateCacheStatus();
            _btnRefreshNow.Enabled = true;
        }

        private void OnOkClick(object sender, EventArgs e)
        {
            PasskeyDataScope scope;
            if (_rdoPasswordless.Checked)
                scope = PasskeyDataScope.PasswordlessOnly;
            else if (_rdoMfa.Checked)
                scope = PasskeyDataScope.MfaOnly;
            else
                scope = PasskeyDataScope.AnySupport;

            _store.Scope              = scope;
            _store.RefreshIntervalHours = (int)_nudInterval.Value;
            _store.VerifyPgpSignature   = _chkPgp.Checked;
        }
    }
}
