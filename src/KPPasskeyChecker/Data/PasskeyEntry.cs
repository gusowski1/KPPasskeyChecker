using System;
using System.Collections.Generic;

namespace KPPasskeyChecker.Data
{
    public sealed class PasskeyEntry
    {
        public string PrimaryDomain { get; set; }
        public IReadOnlyList<string> AdditionalDomains { get; set; }
        public PasskeySupportLevel? Passwordless { get; set; }
        public PasskeySupportLevel? Mfa { get; set; }
        public string DocumentationUrl { get; set; }
        public string RecoveryUrl { get; set; }
        public string Notes { get; set; }
        public ContactInfo Contact { get; set; }
        public IReadOnlyList<string> Regions { get; set; }

        public bool SupportsPasswordless { get { return Passwordless.HasValue; } }
        public bool SupportsMfa { get { return Mfa.HasValue; } }
        public bool SupportsAny { get { return Passwordless.HasValue || Mfa.HasValue; } }

        public PasskeyEntry()
        {
            PrimaryDomain     = string.Empty;
            AdditionalDomains = new string[0];
            Regions           = new string[0];
        }
    }
}
