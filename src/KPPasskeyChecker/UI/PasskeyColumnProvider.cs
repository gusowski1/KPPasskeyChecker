using System;
using System.Drawing;
using System.Windows.Forms;
using KeePass.UI;
using KeePassLib;
using KPPasskeyChecker.Data;
using KPPasskeyChecker.Shared.DomainMatching;
using KPPasskeyChecker.Shared.KeePassUi;

namespace KPPasskeyChecker.UI
{
    public sealed class PasskeyColumnProvider : ColumnProvider
    {
        public const string ColumnName = "Passkey Support";

        // The KeePass main-window icon, shown in the entry-detail window's title bar so it looks
        // like a native KeePass dialog. Supplied by the plugin (which has the IPluginHost); may be
        // null, in which case the detail window hides its title-bar icon.
        private readonly Icon _windowIcon;

        public PasskeyColumnProvider(Icon windowIcon)
        {
            _windowIcon = windowIcon;
        }

        public override string[] ColumnNames
        {
            get { return new string[] { ColumnName }; }
        }

        public override string GetCellData(string strCol, PwEntry pe)
        {
            if (!PasskeyDirectoryService.IsAvailable) return string.Empty;

            PasskeyDirectory dir = PasskeyDirectoryService.Current.Directory;
            if (dir == null) return string.Empty;

            string host = ExtractHost(pe);
            if (host == null) return string.Empty;

            PasskeyEntry entry = Lookup(dir, host);
            return entry == null ? string.Empty : FormatEntry(entry);
        }

        /// <summary>
        /// KeePass calls <see cref="PerformCellAction"/> on double-click / Enter for this column
        /// when this returns true — and it does so even for an empty cell (the call is gated only on
        /// this flag, not on the cell text). So returning true here is enough to also reach the
        /// "no data" dialog for an unmatched domain.
        /// </summary>
        public override bool SupportsCellAction(string strColumnName)
        {
            return strColumnName == ColumnName;
        }

        public override void PerformCellAction(string strColumnName, PwEntry pe)
        {
            if (strColumnName != ColumnName || pe == null) return;

            string host = ExtractHost(pe);
            string domain = host ?? string.Empty;

            EntryDetailModel model;

            if (!PasskeyDirectoryService.IsAvailable
                || PasskeyDirectoryService.Current.Directory == null)
            {
                model = PasskeyDetailModelBuilder.Build(domain, null);
                model.EmptyMessage =
                    "Directory data is not available yet. Open the Passkey Checker settings "
                    + "to check the cache status or refresh now.";
            }
            else
            {
                PasskeyDirectory dir = PasskeyDirectoryService.Current.Directory;
                PasskeyEntry entry = host == null ? null : Lookup(dir, host);

                if (entry != null)
                {
                    // On a match the banner subtitle shows the actually matched directory domain
                    // (entry.PrimaryDomain), which for a subdomain match can differ from the user's
                    // stored host (e.g. host "mail.google.com" matching directory "google.com").
                    // In the no-match branch below we deliberately show the user's host instead.
                    model = PasskeyDetailModelBuilder.Build(entry.PrimaryDomain, entry);
                }
                else
                {
                    model = PasskeyDetailModelBuilder.Build(domain, null);
                    model.EmptyMessage = string.IsNullOrEmpty(domain)
                        ? "This entry has no website URL to look up."
                        : "No data found for this domain in the directory.";
                }
            }

            model.WindowIcon = _windowIcon;

            using (EntryDetailForm form = new EntryDetailForm(model))
                form.ShowDialog();
        }

        private static PasskeyEntry Lookup(PasskeyDirectory dir, string host)
        {
            foreach (string candidate in DomainCandidateGenerator.GetCandidates(host))
            {
                PasskeyEntry entry = dir.FindByDomain(candidate);
                if (entry != null) return entry;
            }
            return null;
        }

        private static string FormatEntry(PasskeyEntry entry)
        {
            if (entry.SupportsPasswordless && entry.SupportsMfa)
                return "Login + 2FA";
            if (entry.SupportsPasswordless)
                return "Login";
            if (entry.SupportsMfa)
                return "2FA";
            // Unreachable for the supported/passwordless/mfa endpoints (every entry there has at
            // least one of the two fields); blank rather than a generic "supported" label.
            return string.Empty;
        }

        private static string ExtractHost(PwEntry pe)
        {
            string url = pe.Strings.ReadSafe(PwDefs.UrlField);
            if (string.IsNullOrEmpty(url) || url.Trim().Length == 0) return null;

            try
            {
                if (!url.Contains("://"))
                    url = "https://" + url;
                return new Uri(url).Host;
            }
            catch
            {
                return null;
            }
        }
    }
}
