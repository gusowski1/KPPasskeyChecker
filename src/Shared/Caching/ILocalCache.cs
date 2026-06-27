// Shared KeeRadar infrastructure — canonical source: KPPasskeyChecker/src/Shared
namespace KeeRadar.Shared.Caching
{
    public interface ILocalCache
    {
        CacheEntry Read(string key);
        void Write(string key, CacheEntry entry);
        void Invalidate(string key);
    }
}
