using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

// Consume bytes produced by the Rust Book and Futures encoder, using the real
// production formatter and accumulator rather than a second bar algorithm.
var input = JsonDocument.Parse(File.ReadAllText(args[0])).RootElement;
var packets = (input.ValueKind == JsonValueKind.Array ? input : input.GetProperty("prices")).Deserialize<string[]>()!;
var events = packets.Select(p => MessagePackBinarySerializer.Shared
    .Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(Convert.FromBase64String(p))!).ToArray();
var date = new DateOnly(2026, 10, 27);
var calendar = new CmeFuturesMarketSessionCalendar();
var end = events[^1].Price.Trade!.Value.EventTimestamp.AddMinutes(1);
FuturesTradeSessionBarAccumulator Create() => new(calendar,
    new PrefixFuturesTradeSessionBarSeriesResolver(new Dictionary<string, MarketSeriesIdentity> {
        ["ES"] = MarketSeriesIdentity.ForFuturesSeries(new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1))
    }), TimeProvider.System);
var bars = Create();
using var cache = new TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCache();
var mirror = new MarketDataReadMirror(cache);
var worker = Guid.NewGuid(); var generation = Guid.NewGuid();
DatasetPublicationEnvelope Frame(FuturesMarketPriceUpdatedRealtimeEvent value, long sequence) => new() {
    Dataset = "GLBX.MDP3", ValueDate = date, WorkerInstanceId = worker,
    GenerationId = generation, ManifestRevision = 1, PublicationSequence = sequence,
    Kind = DatasetPublicationKind.MarketPrice,
    Payload = MessagePack.MessagePackSerializer.Serialize(value, MessagePackBinarySerializer.ContentOptions)
};
var manifest = new DatasetSubscriptionManifest("GLBX.MDP3", date, 1, [new() {
    DomainContractId = "ES20261218", ProviderContractName = "ESZ6", RootSymbol = "ES",
    AssetTypeId = AssetTypeId.Futures, Dataset = "GLBX.MDP3", OnTheRun = true
}]);
// A concurrent query snapshot is deliberately ahead of all queued executions.
mirror.Apply(new(new(true, date, [new("Futures", "GLBX.MDP3", worker, generation,
    1, true, true, "Healthy", null, date)], Guid.NewGuid(), 1), [manifest], [], [Frame(events[^1], 100)]));
for (var index = 0; index < events.Length; index++) {
    var frame = Frame(events[index], index + 1);
    if (!mirror.Accept(frame)) throw new InvalidDataException("Read snapshot consumed a queued execution.");
    bars.Accept(events[index]); bars.Accept(events[index]);
    if (mirror.Accept(frame)) throw new InvalidDataException("Duplicate execution was admitted.");
}
var closed = bars.CloseThrough(end);
var first = closed.Single(b => b.TimeFrame == TimeFrameType.FifteenSeconds);
if (first.TradeCount != 3 || first.Volume != 6 || first.Open != 6000m || first.Close != 6000.25m)
    throw new InvalidDataException("Native stream lost execution continuity, price or volume.");
// A genuine missing execution must still invalidate the epoch.
var gap = Create(); gap.Accept(events[0]); gap.Accept(events[2]);
if (gap.CloseThrough(end).Count != 0) throw new InvalidDataException("Actual gaps were hidden.");
Console.WriteLine("PASS Rust executions -> C# MessagePack -> snapshot-ahead read mirror -> production bars: unchanged prices, duplicate suppression, exact OHLCV and genuine-gap rejection.");

if (input.ValueKind == JsonValueKind.Object)
{
    // Native statistics must advance the actual API read mirror before each
    // price observation, even when consecutive executions have the same price.
    var statisticsPackets = input.GetProperty("statistics").Deserialize<string[]>()!;
    using var volumeCache = new TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCache();
    var volumeMirror = new MarketDataReadMirror(volumeCache);
    volumeMirror.Apply(new(new(true, date, [new("Futures", "GLBX.MDP3", worker, generation,
        1, true, true, "Healthy", null, date)], Guid.NewGuid(), 1), [manifest], [], []));
    for (var index = 0; index < statisticsPackets.Length; index++)
    {
        var bytes = Convert.FromBase64String(statisticsPackets[index]);
        var statistics = MessagePackBinarySerializer.Shared.Deserialize<TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.FuturesSessionStatisticsUpdatedRealtimeEvent>(bytes)!;
        var envelope = Frame(events[index], index * 2 + 1) with { Kind = DatasetPublicationKind.SessionStatistics, Payload = bytes };
        if (!volumeMirror.Accept(envelope)) throw new InvalidDataException("Native statistics were rejected.");
        // This legacy bool reports complete OHLC, while the out value also
        // carries independently usable volume consumed by the EOD event handler.
        var complete = volumeMirror.TryGetFuturesSessionStatistics("ES20261218", out var current);
        if (complete != current.HasPriceStatistics || !current.HasVolume
            || current.ValueDate != date || current.Volume != (index + 1) * (index + 2) / 2
            || current.Volume != statistics.Statistics.Volume)
            throw new InvalidDataException("Native cumulative volume failed API mirror publication.");
        if (!volumeMirror.Accept(Frame(events[index], index * 2 + 2)))
            throw new InvalidDataException("Price following its volume statistics was rejected.");
    }
    Console.WriteLine("PASS Rust session statistics -> actual C# formatter -> API mirror: exact cumulative volume 1,3,6, unchanged-price executions and price ordering.");
}
