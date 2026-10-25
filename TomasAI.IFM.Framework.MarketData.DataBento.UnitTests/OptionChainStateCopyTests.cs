using TomasAI.IFM.Shared.MarketData.Definitions;
using System.Buffers;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;
using Xunit.Abstractions;

namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class OptionChainStateCopyTests(ITestOutputHelper output)
{
    [Fact]
    public void Ordered_copy_preserves_state_and_reuses_memory_without_full_chain_materialization()
    {
        var store = new OptionChainStateStore();
        var key = new OptionChainSessionKey("ES", new(2026, 12, 18));
        var routes = Enumerable.Range(0, 256).Select(i => new DatabentoOptionChainRoute
        {
            FuturesOptionContractId = $"ES-{i}", Definition = new OptionContractDefinition
            {
                Dataset = "GLBX.MDP3", RawSymbol = $"ES-{i}", Underlying = "ES", Ticker = "ES",
                Instrument = default, Right = OptionRightSelection.Call, MaturityDate = key.MaturityDate,
                StrikePrice = 5000 + i
            }
        }).ToArray();
        store.Create(key, routes);
        var ids = routes.Reverse().Select(x => x.FuturesOptionContractId).ToArray();
        var buffer = ArrayPool<OptionChainContractState>.Shared.Rent(ids.Length);
        try
        {
            Assert.Equal(256, store.GetCount(key));
            Assert.True(store.TryCopyContracts(key, ids, buffer));
            for (var i = 0; i < ids.Length; i++)
            {
                Assert.True(store.TryGet(key, ids[i], out var expected));
                Assert.Equal(expected, buffer[i]);
            }
            Assert.False(store.TryCopyContracts(key, ["missing"], buffer));
            var missing = ids.ToArray(); missing[0] = "missing";
            Assert.False(store.TryCopyContracts(key, missing, buffer));
            Assert.Throws<ArgumentException>(() => { store.TryCopyContracts(key, ids, buffer.AsSpan(0, 1)); });
            var start = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 30; i++)
            {
                _ = store.GetSession(key).Count;
                _ = store.GetSession(key).ToDictionary(x => x.Route.FuturesOptionContractId, StringComparer.Ordinal);
            }
            var oldBytes = GC.GetAllocatedBytesForCurrentThread() - start;
            start = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 30; i++)
            {
                _ = store.GetCount(key);
                Assert.True(store.TryCopyContracts(key, ids, buffer));
            }
            var newBytes = GC.GetAllocatedBytesForCurrentThread() - start;
            output.WriteLine($"256 contracts, 30 chain reads: sorted arrays/dictionary {oldBytes:N0} allocated bytes -> coherent pooled copy {newBytes:N0}.");
            Assert.True(newBytes < oldBytes / 10);
            // Returning the capture buffer cannot mutate the published value copies.
            var retained = buffer[0];
            Array.Clear(buffer);
            Assert.Equal(ids[0], retained.Route.FuturesOptionContractId);
        }
        finally { ArrayPool<OptionChainContractState>.Shared.Return(buffer, clearArray: true); }
        store.Remove(key);
        Assert.Equal(0, store.GetCount(key));
        Assert.False(store.TryCopyContracts(key, ids, new OptionChainContractState[ids.Length]));
    }
}
