using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class DatabentoTickContractMappingStoreTests
{
    [Fact]
    public void Mapping_is_definition_date_scoped_and_idempotent()
    {
        var store = new DatabentoTickContractMappingStore();
        var date = new DateOnly(2026, 8, 10);
        store.SetTickMapping("GLBX.MDP3", date, 7, 42, "ESU6", AssetTypeId.Futures);
        store.SetTickMapping("GLBX.MDP3", date, 7, 42, "ESU6", AssetTypeId.Futures);

        Assert.True(store.TryGetMapping(
            "GLBX.MDP3", date, new InstrumentKey(7, 42), out var mapping));
        Assert.Equal("ESU6", mapping.ContractId);
        Assert.False(store.TryGetMapping(
            "GLBX.MDP3", date.AddDays(1), new InstrumentKey(7, 42), out _));
    }

    [Fact]
    public void Conflicting_mapping_is_rejected()
    {
        var store = new DatabentoTickContractMappingStore();
        var date = new DateOnly(2026, 8, 10);
        store.SetTickMapping("GLBX.MDP3", date, 7, 42, "ESU6", AssetTypeId.Futures);

        Assert.Throws<InvalidOperationException>(() => store.SetTickMapping(
            "GLBX.MDP3", date, 7, 42, "NQU6", AssetTypeId.Futures));
    }
}
