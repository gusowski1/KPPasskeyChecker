using System;
using System.Collections;
using System.Collections.Generic;

namespace KPPasskeyChecker.Data
{
    internal static class PasskeyEntryMapper
    {
        public static PasskeyEntry Map(string domain, Dictionary<string, object> data)
        {
            return new PasskeyEntry
            {
                PrimaryDomain     = domain,
                AdditionalDomains = GetStringList(data, "additional-domains"),
                Passwordless      = ParseLevel(GetString(data, "passwordless")),
                Mfa               = ParseLevel(GetString(data, "mfa")),
                DocumentationUrl  = GetString(data, "documentation"),
                RecoveryUrl       = GetString(data, "recovery"),
                Notes             = GetString(data, "notes"),
                Regions           = GetStringList(data, "regions"),
                Contact           = MapContact(data)
            };
        }

        private static ContactInfo MapContact(Dictionary<string, object> data)
        {
            object raw;
            if (!data.TryGetValue("contact", out raw)) return null;
            Dictionary<string, object> c = raw as Dictionary<string, object>;
            if (c == null) return null;

            return new ContactInfo
            {
                Twitter  = GetString(c, "twitter"),
                Facebook = GetString(c, "facebook"),
                Email    = GetString(c, "email"),
                Form     = GetString(c, "form"),
                Language = GetString(c, "language")
            };
        }

        private static string GetString(Dictionary<string, object> d, string key)
        {
            object val;
            if (d.TryGetValue(key, out val))
                return val as string;
            return null;
        }

        private static IReadOnlyList<string> GetStringList(Dictionary<string, object> d, string key)
        {
            object val;
            if (!d.TryGetValue(key, out val)) return new string[0];
            ArrayList list = val as ArrayList;
            if (list == null) return new string[0];

            var result = new List<string>(list.Count);
            foreach (var item in list)
            {
                string s = item as string;
                if (!string.IsNullOrEmpty(s)) result.Add(s);
            }
            return result;
        }

        private static PasskeySupportLevel? ParseLevel(string value)
        {
            if (value == null) return null;
            if (value.Equals("required", StringComparison.OrdinalIgnoreCase))
                return PasskeySupportLevel.Required;
            if (value.Equals("allowed", StringComparison.OrdinalIgnoreCase))
                return PasskeySupportLevel.Allowed;
            return null;
        }
    }
}
