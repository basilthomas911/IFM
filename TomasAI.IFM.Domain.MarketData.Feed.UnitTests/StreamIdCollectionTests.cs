using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests;

public sealed class StreamIdCollectionTests
{
    [Fact]
    public void Instances_DoNotReplaceEachOthersState()
    {
        var first = new StreamIdCollection();
        var id = first.Add("ESU6");
        var second = new StreamIdCollection();

        first["ESU6"].Should().Be(id);
        first.Count.Should().Be(1);
        second.Count.Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentAddAndLookup_ProducesStableUniqueIds()
    {
        var collection = new StreamIdCollection();
        var contracts = Enumerable.Range(0, 512).Select(static value => $"ES-{value}").ToArray();

        await Parallel.ForEachAsync(contracts, async (contract, _) =>
        {
            var first = collection.Add(contract);
            await Task.Yield();
            collection[contract].Should().Be(first);
        });

        var ids = contracts.Select(contract => collection[contract]).ToArray();
        ids.Should().OnlyHaveUniqueItems();
        ids.Should().OnlyContain(id => collection.Exists(id));
        collection.Count.Should().Be(contracts.Length);
    }

    [Fact]
    public void Remove_UpdatesBothDirections()
    {
        var collection = new StreamIdCollection();
        var id = collection.Add("VXU6");

        collection.Remove(id);

        collection.Exists(id).Should().BeFalse();
        collection["VXU6"].Should().Be(-1);
        collection.Count.Should().Be(0);
    }
}
