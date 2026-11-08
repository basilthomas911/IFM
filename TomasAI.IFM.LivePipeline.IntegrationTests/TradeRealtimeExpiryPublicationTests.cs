using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class TradeRealtimeExpiryPublicationTests
{
    [Fact]
    public async Task Healthy_async_bursts_drain_without_waiting_a_timer_tick_per_four_publications()
    {
        var (cache, _) = Fixture(64);
        var producer = Substitute.For<IActorProducer>();
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0; var maximum = 0; var count = 0;
        producer.SendAsync<TradeRealtimeSnapshotChangedEvent, TradeEntityId>(Arg.Any<ActorSubject>(), Arg.Any<TradeRealtimeSnapshotChangedEvent>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask(SendAsync(call.Arg<CancellationToken>())));
        using var service = new TradeRealtimeCacheExpiryService(cache, producer, NullLogger<TradeRealtimeCacheExpiryService>.Instance);
        await service.StartAsync(default);
        try { await delivered.Task.WaitAsync(TimeSpan.FromSeconds(2)); Assert.InRange(maximum, 2, 4); Assert.Equal(64, count); }
        finally { using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await service.StopAsync(stop.Token); }

        async Task SendAsync(CancellationToken token)
        {
            var current = Interlocked.Increment(ref active);
            int previous;
            do { previous = Volatile.Read(ref maximum); if (previous >= current) break; }
            while (Interlocked.CompareExchange(ref maximum, current, previous) != previous);
            try { await Task.Delay(1, token); if (Interlocked.Increment(ref count) == 64) delivered.TrySetResult(); }
            finally { Interlocked.Decrement(ref active); }
        }
    }

    [Fact]
    public async Task A_failed_trade_does_not_block_new_expiry_transitions_for_other_trades()
    {
        var clock = new Clock(); var cache = new TradeRealtimeCacheRegistry(clock);
        var first = Add("first", 1501); clock.Now = clock.Now.AddSeconds(6);
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var independent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var producer = Substitute.For<IActorProducer>();
        producer.SendAsync<TradeRealtimeSnapshotChangedEvent, TradeEntityId>(Arg.Any<ActorSubject>(), Arg.Any<TradeRealtimeSnapshotChangedEvent>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (call.Arg<TradeRealtimeSnapshotChangedEvent>().EntityId == first.Id)
                { attempted.TrySetResult(); return ValueTask.FromException(new IOException("permanently failed first route")); }
                independent.TrySetResult(); return ValueTask.CompletedTask;
            });
        using var service = new TradeRealtimeCacheExpiryService(cache, producer, NullLogger<TradeRealtimeCacheExpiryService>.Instance);
        await service.StartAsync(default);
        try
        {
            await attempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            _ = Add("independent", 1502); clock.Now = clock.Now.AddSeconds(6);
            await independent.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally { using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await service.StopAsync(stop.Token); }

        EstablishedTradeDefinition Add(string contract, int id)
        {
            var date = new DateOnly(2026, 11, 7);
            var trade = new EstablishedTradeDefinition { Id = new(101, 701, 1901, id), StrategyKind = TradeStrategyKind.IronCondor,
                Legs = [new() { TradeLegId = Guid.NewGuid(), ContractId = contract }] };
            cache.Register(trade, date);
            cache.Apply(new FuturesOptionTickDataV2ReadModel(contract, date, 1, new(12, 0), 10, 9, 11, 1, 1, .2, 5000, .4, .01, 12, -3, .1)
            { SourceGeneration = Guid.NewGuid(), ObservedAtUtc = clock.Now, ValidUntilUtc = clock.Now.AddSeconds(5) });
            return trade;
        }
    }

    [Fact]
    public async Task Failed_expiry_publication_retries_the_same_event_identity_without_another_tick()
    {
        var (cache, trade) = Fixture();
        var producer = Substitute.For<IActorProducer>();
        var ids = new System.Collections.Concurrent.ConcurrentQueue<Guid>();
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        producer.SendAsync<TradeRealtimeSnapshotChangedEvent, TradeEntityId>(Arg.Any<ActorSubject>(), Arg.Any<TradeRealtimeSnapshotChangedEvent>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                ids.Enqueue(call.Arg<TradeRealtimeSnapshotChangedEvent>().Id);
                if (ids.Count == 1) return ValueTask.FromException(new IOException("fixture NATS interruption"));
                delivered.TrySetResult(); return ValueTask.CompletedTask;
            });
        using var service = new TradeRealtimeCacheExpiryService(cache, producer, NullLogger<TradeRealtimeCacheExpiryService>.Instance);
        await service.StartAsync(default);
        try { await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await service.StopAsync(stop.Token); }
        Assert.Equal(2, ids.Count); Assert.Single(ids.Distinct());
        Assert.Equal("Stale", cache.Find(trade.Id)!.Read().Legs[0].QuoteStatus);
    }

    [Fact]
    public async Task A_removed_trade_fences_its_failed_pending_publication()
    {
        var (cache, trade) = Fixture();
        var producer = Substitute.For<IActorProducer>();
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        producer.SendAsync<TradeRealtimeSnapshotChangedEvent, TradeEntityId>(Arg.Any<ActorSubject>(), Arg.Any<TradeRealtimeSnapshotChangedEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => { Interlocked.Increment(ref calls); attempted.TrySetResult(); return ValueTask.FromException(new IOException("fixture")); });
        using var service = new TradeRealtimeCacheExpiryService(cache, producer, NullLogger<TradeRealtimeCacheExpiryService>.Instance);
        await service.StartAsync(default);
        try
        {
            await attempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cache.Remove(trade.Id); cache.Register(trade, new(2026, 11, 7));
            await Task.Delay(1500);
            Assert.Equal(1, calls);
            Assert.Equal(0, cache.Find(trade.Id)!.Read().SnapshotRevision);
        }
        finally { using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await service.StopAsync(stop.Token); }
    }

    static (TradeRealtimeCacheRegistry, EstablishedTradeDefinition) Fixture(int count = 1)
    {
        var clock = new Clock();
        var cache = new TradeRealtimeCacheRegistry(clock);
        var date = new DateOnly(2026, 11, 7);
        var trade = new EstablishedTradeDefinition { Id = new(101, 701, 1901, 1501), StrategyKind = TradeStrategyKind.IronCondor,
            Legs = [new() { TradeLegId = Guid.NewGuid(), ContractId = "expiry" }] };
        for (var index = 0; index < count; index++)
        {
            var current = trade with { Id = new(101, 701, 1901, 1501 + index) };
            cache.Register(current, date);
        }
        cache.Apply(new FuturesOptionTickDataV2ReadModel("expiry", date, 1, new(12, 0), 10, 9, 11, 1, 1, .2, 5000, .4, .01, 12, -3, .1)
        { SourceGeneration = Guid.NewGuid(), ObservedAtUtc = clock.Now, ValidUntilUtc = clock.Now.AddSeconds(5) });
        clock.Now = clock.Now.AddSeconds(6);
        return (cache, trade);
    }

    sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
