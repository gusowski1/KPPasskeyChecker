using System;
using System.Collections.Generic;

namespace KPPasskeyChecker.Data
{
    public sealed class PasskeyDirectory
    {
        private readonly Dictionary<string, PasskeyEntry> _index;

        public int Count { get; private set; }

        private PasskeyDirectory(Dictionary<string, PasskeyEntry> index, int count)
        {
            _index = index;
            Count  = count;
        }

        public PasskeyEntry FindByDomain(string domain)
        {
            PasskeyEntry entry;
            return _index.TryGetValue(domain.ToLowerInvariant(), out entry) ? entry : null;
        }

        internal static PasskeyDirectory Build(Dictionary<string, object> raw)
        {
            var index = new Dictionary<string, PasskeyEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in raw)
            {
                Dictionary<string, object> data = kvp.Value as Dictionary<string, object>;
                if (data == null) continue;

                PasskeyEntry entry = PasskeyEntryMapper.Map(kvp.Key, data);
                index[kvp.Key.ToLowerInvariant()] = entry;

                foreach (string extra in entry.AdditionalDomains)
                    if (!string.IsNullOrWhiteSpace(extra))
                        index[extra.ToLowerInvariant()] = entry;
            }

            return new PasskeyDirectory(index, index.Count);
        }
    }
}
