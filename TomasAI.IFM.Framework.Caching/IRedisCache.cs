namespace TomasAI.IFM.Framework.Caching;

public interface IRedisCache
{
    void Set(string key, string value);
    void Set(string key, string value, TimeSpan expiry);
    void Set(string key, string value, DateTimeOffset absoluteExpiry, TimeSpan ttl);
    string? Get(string key);
    bool TryGet(string key, out string? value);
    void Remove(string key);
    long RemoveByPrefix(string prefix);
    Task SetAsync(string key, string value);
    Task SetAsync(string key, string value, TimeSpan expiry);
    Task SetAsync(string key, string value, DateTimeOffset absoluteExpiry, TimeSpan ttl);
    Task<string?> GetAsync(string key);
    long Increment(string key);
    void DeleteAllKeys();
}
