using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using Xunit;

namespace TomasAI.IFM.Domain.OptionPricer.UnitTests;

public class OptionPricerIdentityTests
{
    [Fact]
    public void Equivalent_pricer_identities_share_the_same_dictionary_entry()
    {
        var first = new OptionPricerId(default, default, 1, 100, 50, 30, 16);
        var equivalent = new OptionPricerId(default, default, 1, 100, 50, 30, 16);
        var different = new OptionPricerId(default, default, 2, 100, 50, 30, 16);
        var cache = new Dictionary<OptionPricerId, string> { [first] = "price" };
        Assert.Equal("price", cache[equivalent]);
        Assert.False(cache.ContainsKey(different));
        Assert.True(first == equivalent);
        Assert.False(first == null);
        Assert.False(null == first);
        Assert.True((OptionPricerId?)null == null);
        Assert.False(first.Equals(new object()));
    }

    [Fact]
    public void Equivalent_pool_identities_are_null_safe_and_hash_consistent()
    {
        var first = new OptionPricersId(default, default, 30);
        var equivalent = new OptionPricersId(default, default, 30);
        var cache = new Dictionary<OptionPricersId, string> { [first] = "pool" };
        Assert.Equal("pool", cache[equivalent]);
        Assert.True(first == equivalent);
        Assert.True(first != null);
        Assert.False(null == first);
        Assert.True((OptionPricersId?)null == null);
        Assert.False(first.Equals(new object()));
    }
}
