using MessagePack;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;

// Expected bytes come from the actual production DTO formatters, never Rust.
var rows=new List<object>();
var id=Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
var command=Guid.Parse("ffeeddcc-bbaa-9988-7766-554433221100");
var date=new DateOnly(2026,10,27);
foreach(var kind in new[]{"trade","quote","stats","market","vwap"})
foreach(var variant in Enumerable.Range(0,4)) {
    var contract=variant==3?"VX / test": "ES20261218";
    var observed=variant switch {0=>0L,1=>-1L,2=>17931168001234567L,_=>2534023007999999999L};
    var at=DateTime.UnixEpoch.AddTicks(observed);
    var raw=variant switch {0=>5900250000000L,1=>-1L,2=>long.MaxValue,_=>long.MinValue};
    var publisher=(ushort)(variant==3?105:3);
    var entity=new TickDataEntityId(contract,date,AssetTypeId.Futures);
    var dataset=variant==3?"XCBF.PITCH":"GLBX.MDP3";
    object packet;
    if(kind=="trade") packet=new FuturesTickTradeDataChangedEvent {
        Subject=new(ActorType.Realtime,FuturesTickTradeDataChangedEvent.Actor,FuturesTickTradeDataChangedEvent.Verb,entity.Format()),
        Id=id,CommandId=command,EntityId=entity,AggregateId=entity.Format(),EventSource="TickAggregationService",ReceivedOn=at,
        TickDataId=new(contract,date,65536,at),AssetTypeId=AssetTypeId.Futures,Dataset=dataset,DefinitionDate=date.AddDays(-1),PublisherId=publisher,InstrumentId=42,
        TradeData=new(uint.MaxValue,1793116800000000001,1793116800000000099,4,raw,raw/1000000000m,uint.MaxValue,84,65,255)};
    else if(kind=="quote") {
        var quote=new FuturesTickQuoteData(uint.MaxValue,1793116800000000001,1793116800000000099,4,raw,raw==long.MaxValue?null:raw/1000000000m,uint.MaxValue,127,
            long.MaxValue,null,128,256);
        var quotes=Enumerable.Repeat(quote,variant+1).ToArray();
        packet=new FuturesTickQuoteDataChangedEvent {
            Subject=new(ActorType.Realtime,FuturesTickQuoteDataChangedEvent.Actor,FuturesTickQuoteDataChangedEvent.Verb,entity.Format()),
            Id=id,CommandId=command,EntityId=entity,AggregateId=entity.Format(),EventSource="TickAggregationService",ReceivedOn=at,
            TickDataId=new(contract,date,65536,at),AssetTypeId=AssetTypeId.Futures,Dataset=dataset,DefinitionDate=date.AddDays(-1),PublisherId=publisher,InstrumentId=42,
            EmissionReason=(QuoteEmissionReason)(variant+1),QuoteCount=(ushort)quotes.Length,QuoteData=new(quotes,(ushort)quotes.Length)};
    } else if(kind=="stats") {
        var eod=new FuturesEodDataId(contract,date.AddDays(-1));
        packet=new FuturesSessionStatisticsUpdatedRealtimeEvent {
            Subject=new(ActorType.Realtime,FuturesSessionStatisticsUpdatedRealtimeEvent.Actor,FuturesSessionStatisticsUpdatedRealtimeEvent.Verb,eod.Format()),
            Id=id,CommandId=command,EntityId=eod,AggregateId=eod.Format(),EventSource="TickAggregationService",ReceivedOn=at,
            Statistics=new(contract,date.AddDays(-1),5900.25m,5901.5m,5800m,uint.MaxValue,1793116800000000001,65536,(FuturesSessionVolumeQuality)variant)};
    } else {
        var quote=new FuturesMarketQuoteSnapshot(5900.25m,uint.MaxValue,null,128,127,256,uint.MaxValue,new(at),new(at));
        var trade=new FuturesMarketTradeSnapshot(5900.25m,uint.MaxValue,uint.MaxValue,new(at),new(at),
            (NormalizedTradeAction)1,(NormalizedTradeSide)2,(NormalizedTradeConditionFlags)3,id,65536);
        FuturesVwapSourceCheckpoint? checkpoint=kind=="vwap"?new(){StreamEpochId=id,LastTradeOrdinal=65536,LastTradeSourceSequence=uint.MaxValue,AsOfUtc=new(at),
            CumulativePriceVolume=1234567.890m,CumulativeVolume=65536,EligibleTradeCount=127,RejectedTradeCount=128,LastPrice=5900.250m,
            IsValid=true,IsReplayComplete=true,RecoveryGenerationId=command,InvalidReason=(FuturesVwapSourceInvalidReason)variant}:null;
        packet=new FuturesMarketPriceUpdatedRealtimeEvent {
            Subject=new(ActorType.Realtime,kind=="vwap"?FuturesVwapSourceCheckpoint.Actor:FuturesMarketPriceUpdatedRealtimeEvent.Actor,"Updated",entity.Format()),
            Id=id,CommandId=command,EntityId=entity,AggregateId=entity.Format(),EventSource="TickAggregationService",ReceivedOn=at,
            Price=variant==3?default:new(contract,42,publisher,AssetTypeId.Futures,date,variant==1?null:quote,variant==0?null:trade),
            UpdateSource=(FuturesMarketPriceUpdateSource)2,VwapCheckpoint=checkpoint};
    }
    byte[] Serialize(MessagePackSerializerOptions options)=>packet switch {
        FuturesTickTradeDataChangedEvent trade=>MessagePackSerializer.Serialize(trade,options),
        FuturesTickQuoteDataChangedEvent quote=>MessagePackSerializer.Serialize(quote,options),
        FuturesSessionStatisticsUpdatedRealtimeEvent stats=>MessagePackSerializer.Serialize(stats,options),
        FuturesMarketPriceUpdatedRealtimeEvent market=>MessagePackSerializer.Serialize(market,options),
        _=>throw new InvalidDataException()};
    var inner=Serialize(MessagePackSerializer.DefaultOptions);
    var content=Serialize(MessagePackBinarySerializer.ContentOptions);
    if(!inner.SequenceEqual(content))throw new InvalidDataException("Inner default event contract differs from explicit content options.");
    rows.Add(new{kind,variant,observed,content,inner,wire=Serialize(MessagePackBinarySerializer.Options)});
}
await File.WriteAllTextAsync(args[0],JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS exported {rows.Count} actual C# raw trade/quote/statistics event fixtures.");
