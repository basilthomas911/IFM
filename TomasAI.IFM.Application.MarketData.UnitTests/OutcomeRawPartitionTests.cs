using MessagePack;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class OutcomeRawPartitionTests
{
    static readonly DateOnly Date=new(2026,11,30);
    static readonly DateTime At=new(2026,11,30,14,30,0,DateTimeKind.Utc);
    static FuturesTickTradeDataChangedEvent Trade(string contract="ES20261218")=>new(){TickDataId=new(contract,Date,1,At),AssetTypeId=AssetTypeId.Futures};
    static FuturesTickQuoteDataChangedEvent Quote()=>new(){TickDataId=new("VX20261216",Date,2,At),AssetTypeId=AssetTypeId.Futures,QuoteCount=1,QuoteData=new(new FuturesTickQuoteData[1],1)};
    static DatasetPublicationEnvelope Frame<T>(DatasetPublicationKind kind,T value)=>new(){Dataset="GLBX.MDP3",ValueDate=Date,WorkerInstanceId=Guid.NewGuid(),GenerationId=Guid.NewGuid(),ManifestRevision=1,PublicationSequence=1,Kind=kind,Payload=MessagePackSerializer.Serialize(value)};
    [Fact]
    public void Original_raw_batch_extracts_exact_trade_and_quote_keys_and_safe_legacy_partitions()
    {
        var batch=new RawFuturesStorageBatch(Guid.NewGuid(),500,[new(1,Guid.NewGuid(),1,MessagePackSerializer.Serialize(Trade())),new(2,Guid.NewGuid(),2,MessagePackSerializer.Serialize(Quote()))]);
        var inventory=new OutcomeRawPartitions();Assert.True(inventory.AddRecord(new(){Kind="RawFuturesStorage",OriginalWire=MessagePackSerializer.Serialize(batch)}));
        Assert.Equal(8,inventory.Partitions.Count);Assert.Contains(new("tick_trade_data",1,"ES20261218","2026-11-30"),inventory.Partitions);Assert.Contains(new("tick_quote_data",1,"VX20261216","2026-11-30"),inventory.Partitions);
    }
    [Fact]
    public void Compressed_native_transport_and_logical_duplicates_keep_one_exact_partition_set()
    {
        var frame=Frame(DatasetPublicationKind.Trade,Trade());var wire=MessagePackSerializer.Serialize(frame,MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
        var group=MessagePackSerializer.Serialize(new NativeSourceCaptureBatch(2,[new(wire,null)]));var inventory=new OutcomeRawPartitions();
        inventory.AddRecord(new(){Kind="NativeTransport/Futures/Batch",OriginalWire=group});inventory.AddRecord(new(){Kind="NativeSource/Futures/Trade",OriginalWire=wire});inventory.AddRecord(new(){Kind="NativeTransport/Futures/Single",OriginalWire=wire});Assert.Equal(4,inventory.Partitions.Count);
    }
    [Fact]
    public void Native_market_replay_statistics_and_option_evidence_keep_explicit_contract_and_date()
    {
        var inventory=new OutcomeRawPartitions();
        inventory.AddFrame(Frame(DatasetPublicationKind.MarketPrice,new FuturesMarketPriceUpdatedRealtimeEvent{Price=new("MES20261218",1,1,AssetTypeId.Futures,Date,null,null)}));
        inventory.AddFrame(Frame(DatasetPublicationKind.TradeReplayBatch,new FuturesTradeReplayBatchRealtimeEvent{EntityId=new("ES20261218",Date,AssetTypeId.Futures)}));
        inventory.AddFrame(Frame(DatasetPublicationKind.SessionStatistics,new FuturesSessionStatisticsUpdatedRealtimeEvent{Statistics=new("VX20261216",Date,1,2,1,1,1)}));
        var source=new OptionTradeSource("GLBX.MDP3",1,10,"ES-option-call",Date,110,1,99,1788883200000000123,1788883200000000456,Guid.NewGuid(),"fixture-call");
        inventory.AddFrame(Frame(DatasetPublicationKind.OptionTradeEvidence,new OptionTradeEvidence(source,null,null,new DateTimeOffset(At),null,null)));
        Assert.Equal(15,inventory.Partitions.Count);Assert.Contains(new("futures_option_tick_data",2,"ES-option-call","2026-11-30"),inventory.Partitions);
    }
    [Fact]
    public void Complete_initial_manifest_and_contracts_cover_dates_without_rotating_option_observations()
    {
        var inventory=new OutcomeRawPartitions();inventory.AddBaseline(new(new(true,Date,[]),[new("GLBX.MDP3",Date,1,[new(){DomainContractId="ES20261218",ProviderContractName="ESZ6",AssetTypeId=AssetTypeId.Futures,RootSymbol="ES",Dataset="GLBX.MDP3"}])],[new(){ContractId="MES20261218"}],[]));
        inventory.CompleteBaselineDates();Assert.Equal(8,inventory.Partitions.Count);
    }
    [Fact]
    public void Unknown_native_or_raw_kinds_and_missing_identity_fail_closed()
    {
        var inventory=new OutcomeRawPartitions();Assert.Throws<InvalidDataException>(()=>inventory.Add("",Date,AssetTypeId.Futures));Assert.Throws<InvalidDataException>(()=>inventory.Add("ES",Date,AssetTypeId.Unknown));Assert.Throws<InvalidDataException>(()=>new OutcomeRawPartitions(1).Add("ES",Date,AssetTypeId.Futures));
        Assert.Throws<InvalidDataException>(()=>inventory.AddFrame(Frame((DatasetPublicationKind)99,Trade())));
        Assert.Throws<InvalidDataException>(()=>inventory.AddRecord(new(){Kind="RawFuturesStorage",OriginalWire=MessagePackSerializer.Serialize(new RawFuturesStorageBatch(Guid.NewGuid(),1,[new(1,Guid.NewGuid(),99,[])]))}));
    }
}
