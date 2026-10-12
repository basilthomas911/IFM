using System;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Shared.Caching;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.Caching;

public sealed class LocalDataCacheServiceTests
{
    [Fact]
    public void Removing_one_key_preserves_other_keys_and_the_named_cache()
    {
        var cache = new LocalDataCacheService();
        var name = Enum.GetValues<DataCacheName>().First(value => value != DataCacheName.Undefined);
        cache.Add(name, "first", "one");
        cache.Add(name, "second", "two");
        cache.Remove(name, "first");
        Assert.False(cache.Exists(name, "first"));
        Assert.Equal("two", cache.Get<string, string>(name, "second"));
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Concurrent_updates_do_not_drop_replacements_and_clear_removes_all_names()
    {
        var cache = new LocalDataCacheService();
        var name = Enum.GetValues<DataCacheName>().First(value => value != DataCacheName.Undefined);
        Parallel.For(0, 32, index => cache.Update(name, index, $"value-{index}"));
        for (var index = 0; index < 32; index++)
            Assert.Equal($"value-{index}", cache.Get<int, string>(name, index));
        cache.Clear();
        Assert.Equal(0, cache.Count);
    }
}
