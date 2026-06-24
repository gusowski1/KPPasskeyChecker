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
                // The service is fail-soft and normally reports problems via LastError instead of
                // throwing; an exception here is the unexpected case.
                ShowRefreshError("Refresh failed: " + ex.Message);
                _btnRefreshNow.Enabled = true;
                return;
            }

            // RefreshAsync is fail-soft: a network/parse/PGP problem does not throw — it leaves the
            // last known-good (cached) data in place and records the cause in LastError. Surface
            // that here so the user can tell a failed refresh apart from a successful one.
            PasskeyDirectoryService svc = PasskeyDirectoryService.Current;
            if (!string.IsNullOrEmpty(svc.LastError))
                ShowRefreshError(DescribeRefreshFailure(svc));
            else
                UpdateCacheStatus();

            _btnRefreshNow.Enabled = true;
        }

        private void ShowRefreshError(string message)
        {
            _lblCacheStatus.Text = message;
            MessageBox.Show(this, message, "Refresh failed",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // Turns the raw LastError into a user-facing message that names the likely cause (network,
        // data format, or signature verification) and states whether cached data is still in use.
        // Fail-soft and fail-closed behaviour is unchanged — this only describes the outcome.
        private static string DescribeRefreshFailure(PasskeyDirectoryService svc)
        {
            string cause = ClassifyError(svc.LastError);
            string body  = "Could not refresh the passkeys directory (" + cause + ")."
                         + "\r\n\r\nDetails: " + svc.LastError;

            return svc.Directory != null
                ? body + "\r\n\r\nStill showing the last cached data."
                : body + "\r\n\r\nNo cached data is available yet.";
        }

        private static string ClassifyError(string error)
        {
            if (string.IsNullOrEmpty(error)) return "unknown error";

            if (error.IndexOf("PGP verification", StringComparison.OrdinalIgnoreCase) >= 0 ||
                error.IndexOf("Signature verifier", StringComparison.OrdinalIgnoreCase) >= 0)
                return "signature verification failed";

            if (error.IndexOf("parse", StringComparison.OrdinalIgnoreCase) >= 0)
                return "unexpected data format";

            return "network error";
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
