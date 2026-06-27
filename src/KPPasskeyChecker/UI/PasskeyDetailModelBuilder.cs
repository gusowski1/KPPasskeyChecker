using System.Collections.Generic;
using KPPasskeyChecker.Data;
using KeeRadar.Shared.KeePassUi;

namespace KPPasskeyChecker.UI
{
    /// <summary>
    /// Maps a <see cref="PasskeyEntry"/> (or the absence of one) to a plugin-agnostic
    /// <see cref="EntryDetailModel"/> for the shared detail window. A row is added only when its
    /// field is actually present, so missing fields are omitted entirely (no placeholders).
    /// </summary>
    internal static class PasskeyDetailModelBuilder
    {
        private const string Attribution =
            "Data sourced from Passkeys Directory by 2factorauth. (CC BY 4.0)";

        public static EntryDetailModel Build(string domain, PasskeyEntry entry)
        {
            EntryDetailModel model = new EntryDetailModel();
            model.Domain = domain;
            model.BannerTitle = "Passkey Details";
            model.Attribution = Attribution;

            if (entry == null)
                return model; // EmptyMessage left null → form shows the default "no data" text.

            List<EntryDetailRow> rows = new List<EntryDetailRow>();

            if (entry.Passwordless.HasValue)
                rows.Add(new TextDetailRow(
                    "Passwordless login", FormatLevel(entry.Passwordless.Value)));

            if (entry.Mfa.HasValue)
                rows.Add(new TextDetailRow(
                    "As 2nd factor", FormatLevel(entry.Mfa.Value)));

            string regions = FormatRegions(entry.Regions);
            if (!string.IsNullOrEmpty(regions))
                rows.Add(new TextDetailRow("Regions", regions));

            if (!string.IsNullOrEmpty(entry.DocumentationUrl))
                rows.Add(new LinkDetailRow("Documentation", entry.DocumentationUrl));

            if (!string.IsNullOrEmpty(entry.RecoveryUrl))
                rows.Add(new LinkDetailRow("Recovery", entry.RecoveryUrl));

            if (!string.IsNullOrEmpty(entry.Notes))
                rows.Add(new NotesDetailRow("Notes", entry.Notes));

            model.Rows = rows;
            return model;
        }

        /// <summary>Maps the support level enum to a human-readable word.</summary>
        internal static string FormatLevel(PasskeySupportLevel level)
        {
            switch (level)
            {
                case PasskeySupportLevel.Required: return "Required";
                case PasskeySupportLevel.Allowed:  return "Allowed";
                default:                           return level.ToString();
            }
        }

        /// <summary>
        /// Formats the region list. A leading "-" on the values marks them as exclusions, e.g.
        /// ["-jp"] → "All regions except: JP"; a positive list is comma-joined and upper-cased.
        /// </summary>
        internal static string FormatRegions(IReadOnlyList<string> regions)
        {
            if (regions == null || regions.Count == 0) return string.Empty;

            bool exclusion = false;
            List<string> codes = new List<string>(regions.Count);
            foreach (string region in regions)
            {
                if (string.IsNullOrEmpty(region)) continue;
                string code = region.Trim();
                if (code.Length == 0) continue;
                if (code[0] == '-')
                {
                    exclusion = true;
                    code = code.Substring(1);
                }
                if (code.Length == 0) continue;
                codes.Add(code.ToUpperInvariant());
            }

            if (codes.Count == 0) return string.Empty;

            string joined = string.Join(", ", codes.ToArray());
            return exclusion ? "All regions except: " + joined : joined;
        }
    }
}
