using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
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

    [Fact]
    public void Live_feed_instrument_is_bound_by_raw_symbol_when_catalog_identity_changed()
    {
        var store = new DatabentoTickContractMappingStore();
        var date = new DateOnly(2026, 8, 18);
        var catalogInstrument = new InstrumentKey(106, 180999);
        var liveInstrument = new InstrumentKey(106, 181038);
        store.SetTickMapping(
            "XCBF.PITCH",
            date,
            catalogInstrument.PublisherId,
            catalogInstrument.InstrumentId,
            "VXU6",
            AssetTypeId.Futures,
            new TickerContractDetails
            {
                ContractId = "VXU6",
                InstrumentId = catalogInstrument.InstrumentId,
                PublisherId = catalogInstrument.PublisherId,
                AssetTypeId = AssetTypeId.Futures,
                Dataset = "XCBF.PITCH",
                DefinitionDate = date,
                ProviderContractId = "VXU6",
                LocalSymbol = "VXU6"
            });

        var resolved = store.TryResolveFeedMapping(
            "XCBF.PITCH",
            date,
            new TickerInstrumentRegistration("VXU6", "VXU6", liveInstrument),
            out var mapping);

        Assert.True(resolved);
        Assert.Equal("VXU6", mapping.ContractId);
        Assert.Equal(liveInstrument.PublisherId, mapping.PublisherId);
        Assert.Equal(liveInstrument.InstrumentId, mapping.InstrumentId);
        Assert.Equal(liveInstrument.PublisherId, mapping.ContractDetails!.PublisherId);
        Assert.Equal(liveInstrument.InstrumentId, mapping.ContractDetails.InstrumentId);
        Assert.True(store.TryGetMapping("XCBF.PITCH", date, liveInstrument, out var cached));
        Assert.Equal(mapping, cached);
    }
}
