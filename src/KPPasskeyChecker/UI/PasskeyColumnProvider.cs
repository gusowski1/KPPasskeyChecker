using System;
using KeePass.UI;
using KeePassLib;
using KPPasskeyChecker.Data;
using KPPasskeyChecker.Shared.DomainMatching;

namespace KPPasskeyChecker.UI
{
    public sealed class PasskeyColumnProvider : ColumnProvider
    {
        public const string ColumnName = "Passkey Support";

        public override string[] ColumnNames
        {
            get { return new string[] { ColumnName }; }
        }

        public override string GetCellData(string strCol, PwEntry pe)
        {
            if (!PasskeyDirectoryService.IsAvailable) return string.Empty;

            PasskeyDirectory dir = PasskeyDirectoryService.Current.Directory;
            if (dir == null) return string.Empty;

            string url = pe.Strings.ReadSafe(KeePassLib.PwDefs.UrlField);
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;

            string host = ExtractHost(url);
            if (host == null) return string.Empty;

            foreach (string candidate in DomainCandidateGenerator.GetCandidates(host))
            {
                PasskeyEntry entry = dir.FindByDomain(candidate);
                if (entry == null) continue;
                return FormatEntry(entry);
            }

            return string.Empty;
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

        private static string ExtractHost(string url)
        {
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
