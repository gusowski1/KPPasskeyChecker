// Shared KeeRadar infrastructure — canonical source: KPPasskeyChecker/src/Shared. Edit only there; propagate to consumer repos via sync-shared.ps1. Do not edit synced copies.
namespace KeeRadar.Shared.Caching
{
    public interface ILocalCache
    {
        CacheEntry Read(string key);
        void Write(string key, CacheEntry entry);
        void Invalidate(string key);
    }
}
