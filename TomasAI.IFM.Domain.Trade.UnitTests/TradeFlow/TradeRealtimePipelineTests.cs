using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class TradeRealtimePipelineTests
{
    [Fact]
    public async Task Pricing_completion_routes_whole_trade_snapshot_across_wire_without_position_command()
    {
        var now = DateTimeOffset.UtcNow;
        var date = DateOnly.FromDateTime(now.UtcDateTime);
        var trade = new EstablishedTradeDefinition
        {
            Id = new(101, 701, 1901, 1501), StrategyKind = TradeStrategyKind.IronCondor,
            Legs = Enumerable.Range(0, 4).Select(i => new TradeLegDefinition
            {
                TradeLegId = Guid.NewGuid(), ContractId = $"option-{i}", PutCall = (byte)(i < 2 ? 2 : 1),
                SignedQuantity = i % 2 == 0 ? -2 : 2, CashMultiplier = 50
            }).ToArray()
        };
        var registry = new TradeRealtimeCacheRegistry();
        registry.Register(trade, date);
        var context = Substitute.For<IFuturesOptionRealtimeContext>();
        context.RealtimeCache.Returns(registry);
        context.Logger.Returns(Substitute.For<ILogger<FuturesOptionRealtimeActor>>());
        context.RouteIndex.Returns(new TomasAI.IFM.Domain.Trade.Futures.Realtime.Model.ContractIdRouteIndex(4096));
        context.ActorService.Returns(Substitute.For<IActorService>());
        var quote = new FuturesOptionTickDataV2ReadModel("option-0", date, 7, new(15, 0), 10,
            9, 11, 2, 3, .2, 5000, -.4, .01, 12, -3, .1)
        {
            SourceGeneration = Guid.NewGuid(), ObservationRevision = 1, ObservedAtUtc = now,
            ValidUntilUtc = now.AddSeconds(5), GreeksAvailable = false
        };
        registry.Apply(quote);
        var completed = quote with { IsPricingUpdate = true, ObservationRevision = 2, GreeksAvailable = true, CalculatedAtUtc = now };
        var changed = new FuturesTickTradeDataChangedEvent
        {
            Id = Guid.NewGuid(), ReceivedOn = now.UtcDateTime,
            OptionMarketPriceObservation = new("ES", OptionMarketPriceBasis.QuoteMidpoint, completed, 7, now.UtcDateTime)
        };
        await changed.ExecuteAsync(context);
        var snapshot = registry.Find(trade.Id)!.Read();
        Assert.Equal(4, snapshot.Legs.Length);
        Assert.Equal("Available", snapshot.Legs[0].PricingStatus);
        Assert.Null(snapshot.Legs[1].Observation);
        Assert.Empty(context.ActorService.ReceivedCalls());
        var bytes = MessagePackBinarySerializer.Shared.Serialize(TradeRealtimeSnapshotChangedEvent.From(snapshot))!;
        var message = MessagePackBinarySerializer.Shared.Deserialize<TradeRealtimeSnapshotChangedEvent>(bytes)!;
        var mirror = new TradeRealtimeHotCache(trade, date);
        Assert.True(mirror.AcceptSnapshot(message.Snapshot));
        Assert.Equal(snapshot.SnapshotRevision, mirror.Read().SnapshotRevision);
        Assert.Equal(12, mirror.Read().Legs[0].Observation!.Vega);
        Assert.False(mirror.AcceptSnapshot(message.Snapshot with { SnapshotRevision = 1 }));
    }

    [Fact]
    public void Shared_contract_fans_out_and_closing_one_trade_releases_only_its_cache()
    {
        var registry = new TradeRealtimeCacheRegistry();
        var date = new DateOnly(2026, 10, 16);
        var trade = new EstablishedTradeDefinition { Id = new(101, 701, 1901, 1501), StrategyKind = TradeStrategyKind.IronCondor,
            Legs = [new() { TradeLegId = Guid.NewGuid(), ContractId = "shared", SignedQuantity = -2 }] };
        var first = registry.Register(trade, date);
        registry.Register(trade with { Id = new(101, 701, 1901, 1502) }, date);
        var observation = new FuturesOptionTickDataV2ReadModel("shared", date, 1, new(15, 0), 10, 9, 11, 1, 1, .2, 5000, -.4, .01, 12, -3, .1);
        Assert.Equal(2, registry.Apply(observation).Length);
        registry.Remove(trade.Id);
        Assert.False(first.Apply(observation with { TickId = 2 }));
        Assert.Single(registry.Apply(observation with { TickId = 2 }));
        Assert.Null(registry.Find(trade.Id));
    }

    [Fact]
    public void Session_cannot_rewind_and_composition_change_replaces_contract_routes()
    {
        var registry = new TradeRealtimeCacheRegistry();
        var date = new DateOnly(2026, 10, 16);
        var trade = new EstablishedTradeDefinition { Id = new(101, 701, 1901, 1501), StrategyKind = TradeStrategyKind.IronCondor,
            EvidenceRevision = 1, Legs = [new() { TradeLegId = Guid.NewGuid(), ContractId = "old" }] };
        var first = registry.Register(trade, date);
        Assert.Same(first, registry.Register(trade, date.AddDays(-1)));
        var next = registry.Register(trade with { EvidenceRevision = 2,
            Legs = [new() { TradeLegId = Guid.NewGuid(), ContractId = "new" }] }, date);
        Assert.NotEqual(first.Read().MonitoringGeneration, next.Read().MonitoringGeneration);
        var observation = new FuturesOptionTickDataV2ReadModel("old", date, 1, new(15, 0), 10, 9, 11, 1, 1, .2, 5000, -.4, .01, 12, -3, .1);
        Assert.Empty(registry.Apply(observation));
        Assert.Single(registry.Apply(observation with { ContractId = "new" }));
        Assert.Same(next, registry.Register(trade, date));
    }
}
