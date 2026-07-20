namespace TomasAI.IFM.Framework.Caching;

public interface IRedisCache
{
    void Set(string key, string value);
    void Set(string key, string value, TimeSpan expiry);
    string? Get(string key);
    bool TryGet(string key, out string? value);
    void Remove(string key);
    Task SetAsync(string key, string value);
    Task SetAsync(string key, string value, TimeSpan expiry);
    Task<string?> GetAsync(string key);
    long Increment(string key);
    void DeleteAllKeys();
}
