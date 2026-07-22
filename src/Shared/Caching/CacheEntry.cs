// Shared KeeRadar infrastructure — canonical source: KPPasskeyChecker/src/Shared. Edit only there; propagate to consumer repos via sync-shared.ps1. Do not edit synced copies.
using System;

namespace KeeRadar.Shared.Caching
{
    public sealed class CacheEntry
    {
        public string Content { get; set; }
        public string ETag { get; set; }
        public DateTimeOffset FetchedAt { get; set; }

        public CacheEntry()
        {
            Content = string.Empty;
        }
    }
}
