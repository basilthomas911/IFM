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
    public void Indexed_expiry_preserves_renewal_exact_deadline_and_removed_generation_fencing()
    {
        var clock = new ExpiryClock();
        var registry = new TradeRealtimeCacheRegistry(clock);
        var date = new DateOnly(2026, 11, 7);
        var trade = new EstablishedTradeDefinition { Id = new(101, 701, 1901, 1501), StrategyKind = TradeStrategyKind.IronCondor,
            Legs = [new() { TradeLegId = Guid.NewGuid(), ContractId = "expiry" }] };
        var cache = registry.Register(trade, date);
        var quote = new FuturesOptionTickDataV2ReadModel("expiry", date, 1, new(12, 0), 10, 9, 11, 1, 1, .2, 5000, .4, .01, 12, -3, .1)
        { SourceGeneration = Guid.NewGuid(), ObservedAtUtc = clock.Now, ValidUntilUtc = clock.Now.AddSeconds(5) };
        registry.Apply(quote); Assert.Empty(registry.Expire());
        clock.Now = quote.ValidUntilUtc;
        registry.Expire(); // Equality must not drop the schedule or spin if still fresh at the boundary.
        clock.Now = quote.ValidUntilUtc.AddTicks(1);
        registry.Expire();
        Assert.Equal("Stale", cache.Read().Legs[0].QuoteStatus);
        Assert.Empty(registry.Expire());
        registry.Apply(quote with { TickId = 2, ObservationRevision = 2, ObservedAtUtc = clock.Now, ValidUntilUtc = clock.Now.AddSeconds(10) });
        Assert.Empty(registry.Expire());
        registry.Remove(trade.Id);
        var replacement = registry.Register(trade, date);
        clock.Now = clock.Now.AddMinutes(1);
        Assert.Empty(registry.Expire());
        Assert.Equal(0, replacement.Read().SnapshotRevision);
    }

    [Fact]
    public void Quote_renewal_postpones_expiry_and_burst_deadlines_publish_each_trade_once()
    {
        var clock = new ExpiryClock();
        var registry = new TradeRealtimeCacheRegistry(clock);
        var date = new DateOnly(2026, 11, 7);
        for (var index = 0; index < 64; index++)
        {
            registry.Register(new EstablishedTradeDefinition { Id = new(101, 701, 1901, index + 1501),
                StrategyKind = TradeStrategyKind.IronCondor,
                Legs = [new() { TradeLegId = Guid.NewGuid(), ContractId = "burst" }] }, date);
        }
        var quote = new FuturesOptionTickDataV2ReadModel("burst", date, 1, new(12, 0), 10, 9, 11, 1, 1, .2, 5000, .4, .01, 12, -3, .1)
        { SourceGeneration = Guid.NewGuid(), ObservedAtUtc = clock.Now, ValidUntilUtc = clock.Now.AddSeconds(5) };
        registry.Apply(quote); registry.Expire();
        clock.Now = clock.Now.AddSeconds(4);
        registry.Apply(quote with { TickId = 2, ObservationRevision = 2, ObservedAtUtc = clock.Now, ValidUntilUtc = clock.Now.AddSeconds(10) });
        clock.Now = clock.Now.AddSeconds(2); Assert.Empty(registry.Expire());
        clock.Now = clock.Now.AddSeconds(11);
        var expired = registry.Expire();
        Assert.Equal(64, expired.Length);
        Assert.Equal(64, expired.Select(snapshot => snapshot.TradeId).Distinct().Count());
        Assert.Empty(registry.Expire());
    }

    [Fact]
    public void Dense_expiry_preserves_blocked_renewed_and_replaced_trade_deadlines()
    {
        var clock = new ExpiryClock();
        var registry = new TradeRealtimeCacheRegistry(clock);
        var date = new DateOnly(2026, 11, 7);
        var ids = new List<TradeEntityId>();
        for (var index = 0; index < 128; index++)
        {
            var trade = new EstablishedTradeDefinition { Id = new(101, 701, 1901, 1501 + index),
                StrategyKind = TradeStrategyKind.IronCondor,
                Legs = [new() { TradeLegId = Guid.NewGuid(), ContractId = "dense-" + index }] };
            ids.Add(trade.Id);
            registry.Register(trade, date);
            var quote = new FuturesOptionTickDataV2ReadModel("dense-" + index, date, 1, new(12, 0), 10, 9, 11, 1, 1, .2, 5000, .4, .01, 12, -3, .1)
            { SourceGeneration = Guid.NewGuid(), ObservedAtUtc = clock.Now, ValidUntilUtc = clock.Now.AddSeconds(index == 127 ? 60 : 5) };
            registry.Apply(quote);
        }
        Assert.Empty(registry.Expire());
        registry.Remove(ids[126]);
        var replacement = registry.Register(new EstablishedTradeDefinition { Id = ids[126],
            StrategyKind = TradeStrategyKind.IronCondor, Legs = [new() { ContractId = "replacement" }] }, date);
        clock.Now = clock.Now.AddSeconds(6);
        var blocked = new HashSet<TradeEntityId> { ids[0] };
        var expired = registry.Expire(blocked);
        Assert.Equal(125, expired.Length);
        Assert.DoesNotContain(expired, value => blocked.Contains(value.TradeId) || value.TradeId == ids[126] || value.TradeId == ids[127]);
        Assert.Equal(0, replacement.Read().SnapshotRevision);
        Assert.Empty(registry.Expire(blocked));
        Assert.Equal(ids[0], Assert.Single(registry.Expire()).TradeId);
        clock.Now = clock.Now.AddSeconds(60);
        Assert.Equal(ids[127], Assert.Single(registry.Expire()).TradeId);
        Assert.Empty(registry.Expire());
    }

    sealed class ExpiryClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void Live_position_and_plan_publications_both_include_backend_history_valuation()
    {
        var date = new DateOnly(2026, 10, 23);
        var trade = new EstablishedTradeDefinition { Id = new(101, 701, 1901, 1501),
            StrategyKind = TradeStrategyKind.IronCondor,
            Legs = Enumerable.Range(0, 4).Select(i => new TradeLegDefinition {
                TradeLegId = Guid.NewGuid(), ContractId = $"option-{i}",
                Expiry = date.AddDays(30), SignedQuantity = i % 2 == 0 ? -1 : 1 }).ToArray() };
        var cache = new TradeRealtimeHotCache(trade, date);
        for (var sequence = 1; sequence <= 4; sequence++)
        {
            var position = new StrategyPositionSnapshot { Id = StrategyPositionId.Create(trade.Id, trade.StrategyKind),
                StrategyKind = trade.StrategyKind, ValueDate = date, Phase = StrategyPositionPhase.MarkToMarket,
                PositionSequence = sequence, RouteGeneration = 1, AsOfUtc = DateTime.UtcNow,
                Legs = trade.Legs.Select((leg, i) => new StrategyPositionLeg { TradeLegId = leg.TradeLegId,
                    ContractId = leg.ContractId, SignedQuantity = leg.SignedQuantity,
                    CurrentPrice = i % 2 == 0 ? 20 + sequence : 10 }).ToArray() };
            Assert.True(cache.Apply(position));
            Assert.True(cache.Apply(new TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.StrategyTradePlanSnapshot {
                Position = position, ValueDate = date, PlanRevision = sequence,
                IronCondorTradePlanSnapshot = new() { DailyPnl = -100m * sequence } }));
            var published = cache.ReadForPublication();
            var wire = MessagePackBinarySerializer.Shared.Deserialize<TradeRealtimeSnapshot>(
                MessagePackBinarySerializer.Shared.Serialize(published)! )!;
            Assert.NotNull(wire.Position!.HistoryValuation);
            Assert.NotNull(wire.Plan!.Position.HistoryValuation);
            Assert.Equal(date, wire.Plan.Position.HistoryValuation.ValueDate);
            Assert.Equal(-20m - 2 * sequence, wire.Plan.Position.HistoryValuation.NetPrice);
            Assert.Equal(wire.Position.HistoryValuation, wire.Plan.Position.HistoryValuation);
            Assert.Null(position.HistoryValuation); // Only the outgoing backend projection is enriched.
        }
    }

    [Fact]
    public async Task Concurrent_route_changes_and_reads_do_not_publish_retired_cache_updates()
    {
        var registry = new TradeRealtimeCacheRegistry();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var trade = new EstablishedTradeDefinition { Id = new(101, 701, 1901, 1501), StrategyKind = TradeStrategyKind.IronCondor,
            Legs = [new() { TradeLegId = Guid.NewGuid(), ContractId = "shared" }] };
        registry.Register(trade, date);
        long sequence = 0;
        var quote = new FuturesOptionTickDataV2ReadModel("shared", date, 1, new(15, 0), 10, 9, 11, 1, 1, .2, 5000, -.4, .01, 12, -3, .1);
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                registry.Apply(quote with { TickId = Interlocked.Increment(ref sequence) });
                registry.Find(trade.Id)?.Read();
                registry.Expire();
            }
        })).ToArray();
        for (var i = 0; i < 100; i++) { registry.Remove(trade.Id); registry.Register(trade, date); }
        await Task.WhenAll(readers);
        var final = registry.Find(trade.Id)!;
        Assert.Single(registry.Apply(quote with { TickId = Interlocked.Increment(ref sequence) }));
        Assert.Equal(sequence, final.Read().Legs[0].Observation!.TickId);
        registry.Remove(trade.Id);
        Assert.Empty(registry.Apply(quote with { TickId = Interlocked.Increment(ref sequence) }));
        Assert.False(final.Apply(quote with { TickId = Interlocked.Increment(ref sequence) }));
    }

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
        var newest = quote with { TickId = 8, ObservedAtUtc = now.AddMilliseconds(100), ObservationRevision = 2, Vega = 0 };
        registry.Apply(newest);
        var completed = quote with { IsPricingUpdate = true, ObservationRevision = 3, GreeksAvailable = true,
            CalculatedAtUtc = now.AddMilliseconds(200), TheoreticalPrice = 10 };
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
        Assert.Equal(8, mirror.Read().Legs[0].Observation!.TickId);
        Assert.Equal(12, mirror.Read().Legs[0].LastValidRisk!.Vega);
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
