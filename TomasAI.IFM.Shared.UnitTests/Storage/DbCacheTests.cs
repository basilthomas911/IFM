using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.Storage;

public sealed class DbCacheTests
{
    [Fact]
    public async Task Concurrent_lookup_creates_one_value_and_preserves_the_other_partition()
    {
        var cache = new DbCache();
        var creations = 0;
        var values = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
            cache.Get<string, long>(name => name.EntityTypeIdMap, "ES", () =>
            {
                Interlocked.Increment(ref creations);
                return 123;
            }))));
        Assert.All(values, value => Assert.Equal(123, value));
        Assert.Equal(1, creations);
        cache.Load(name => name.EventTypeIdMap, new Dictionary<string, long>(StringComparer.Ordinal) { ["ES"] = 456 });
        cache.Clear(name => name.EntityTypeIdMap);
        Assert.Equal(0, cache.Count(name => name.EntityTypeIdMap));
        Assert.Equal(456, cache.Get<string, long>(name => name.EventTypeIdMap, "ES"));
    }

    [Fact]
    public void Load_replaces_entries_and_copies_the_supplied_dictionary()
    {
        var cache = new DbCache();
        cache.Get<string, long>(name => name.EntityTypeIdMap, "old", () => 1);
        var input = new Dictionary<string, long>(StringComparer.Ordinal) { ["new"] = 2 };
        cache.Load(name => name.EntityTypeIdMap, input);
        input["new"] = 99;
        Assert.Equal(1, cache.Count(name => name.EntityTypeIdMap));
        Assert.Equal(0, cache.Get<string, long>(name => name.EntityTypeIdMap, "old"));
        Assert.Equal(2, cache.Get<string, long>(name => name.EntityTypeIdMap, "new"));
        Assert.Throws<InvalidOperationException>(() => cache.Get<string, int>(name => name.EntityTypeIdMap, "new"));
    }
}
