using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.TickAggregation;

public sealed class BoundedTickAggregationPublisherTests
{
    [Fact]
    public async Task Noncooperative_send_retains_its_quote_lease_until_actual_send_completion()
    {
        var (supervisor, producer) = Setup();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lease = new TestLease();
        var queued = new TestLease();
        var rejected = new TestLease();
        producer.SendAsync<FuturesTickQuoteDataChangedEvent, TickDataEntityId>(
            Arg.Any<ActorSubject>(), Arg.Any<FuturesTickQuoteDataChangedEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => { entered.TrySetResult(); return new ValueTask(release.Task); });
        await using var publisher = new TickAggregationEventPublisher(supervisor,
            policy: new() { Capacity = 1, SendTimeout = TimeSpan.FromMilliseconds(300) });
        await publisher.StartAsync();
        try
        {
            await publisher.PublishAsync(Quote(lease), lease);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await publisher.PublishAsync(Quote(queued), queued);
            await Assert.ThrowsAsync<RealtimeTickPublisherSaturatedException>(() => publisher.PublishAsync(Quote(rejected), rejected).AsTask());
            Assert.Equal(0, rejected.Disposed);
            await Until(() => publisher.GetSnapshot().Faulted);
            Assert.Equal(0, lease.Disposed);
            Assert.Equal(1, queued.Disposed);
            await publisher.StopAsync();
            Assert.Equal(0, lease.Disposed);
        }
        finally { release.TrySetResult(); rejected.Dispose(); }
        await Until(() => lease.Disposed == 1);
        Assert.Equal(1, queued.Disposed);
    }

    static FuturesTickQuoteDataChangedEvent Quote(TestLease lease) => new()
    { QuoteCount = lease.Count, QuoteData = new(lease.Buffer, lease.Count) };

    sealed class TestLease : ITickQuoteBufferLease
    {
        int disposed;
        public int Disposed => Volatile.Read(ref disposed);
        public FuturesTickQuoteData[] Buffer { get; } = new FuturesTickQuoteData[1];
        public ushort Count { get; private set; } = 1;
        public void SetCount(ushort count) => Count = count;
        public void Dispose() => Interlocked.Increment(ref disposed);
    }

    [Fact]
    public async Task Transport_failure_discards_backlog_and_explicit_recovery_sends_only_fresh_output()
    {
        var (supervisor, producer) = Setup();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Price();
        var old = Price();
        producer.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
            first.Subject, first, Arg.Any<CancellationToken>()).Returns(_ =>
            { entered.TrySetResult(); return new ValueTask(release.Task); });
        await using var publisher = new TickAggregationEventPublisher(supervisor, policy: new());
        await publisher.StartAsync();
        await publisher.PublishAsync(first);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await publisher.PublishAsync(old);
        release.SetException(new IOException("injected outage"));
        await Until(() => publisher.GetSnapshot().CanRecover);
        Assert.Equal(0, publisher.GetSnapshot().Depth);
        Assert.Equal(2, publisher.GetSnapshot().Failed);
        await publisher.StartAsync();
        var fresh = Price();
        await publisher.PublishAsync(fresh);
        await Until(() => publisher.GetSnapshot().Published == 1);
        await producer.DidNotReceive().SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
            old.Subject, old, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cooperative_send_deadline_faults_without_uncontained_work()
    {
        var (supervisor, producer) = Setup();
        producer.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
            Arg.Any<ActorSubject>(), Arg.Any<FuturesMarketPriceUpdatedRealtimeEvent>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask(Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>())));
        await using var publisher = new TickAggregationEventPublisher(supervisor,
            policy: new() { SendTimeout = TimeSpan.FromMilliseconds(100), CancellationGracePeriod = TimeSpan.FromSeconds(1) });
        await publisher.StartAsync();
        await publisher.PublishAsync(Price());
        await Until(() => publisher.GetSnapshot().CanRecover);
        Assert.Equal(RealtimeTickPublisherFailure.SendTimedOut, publisher.GetSnapshot().Failure);
        Assert.False(publisher.GetSnapshot().UncontainedSend);
    }

    [Fact]
    public async Task Noncooperative_send_latches_and_never_starts_an_overlapping_sender()
    {
        var (supervisor, producer) = Setup();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        producer.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
            Arg.Any<ActorSubject>(), Arg.Any<FuturesMarketPriceUpdatedRealtimeEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => { entered.TrySetResult(); return new ValueTask(release.Task); });
        await using var publisher = new TickAggregationEventPublisher(supervisor,
            policy: new() { SendTimeout = TimeSpan.FromMilliseconds(250), CancellationGracePeriod = TimeSpan.FromMilliseconds(50) });
        await publisher.StartAsync();
        try
        {
            await publisher.PublishAsync(Price());
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await publisher.PublishAsync(Price());
            await Until(() => publisher.GetSnapshot().Faulted);
            Assert.True(publisher.GetSnapshot().UncontainedSend);
            Assert.Equal(0, publisher.GetSnapshot().Depth);
            await Assert.ThrowsAsync<RealtimeTickPublisherUnavailableException>(() => publisher.StartAsync().AsTask());
            await publisher.StopAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        }
        finally { release.TrySetResult(); }
        await Until(() => !publisher.GetSnapshot().UncontainedSend);
        await Assert.ThrowsAsync<RealtimeTickPublisherUnavailableException>(() => publisher.StartAsync().AsTask());
        await producer.Received(1).SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
            Arg.Any<ActorSubject>(), Arg.Any<FuturesMarketPriceUpdatedRealtimeEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Expired_queue_is_discarded_instead_of_replayed()
    {
        var (supervisor, producer) = Setup();
        var time = new QueueTime();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Price();
        var old = Price();
        producer.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
            first.Subject, first, Arg.Any<CancellationToken>()).Returns(_ =>
            { entered.TrySetResult(); return new ValueTask(release.Task); });
        await using var publisher = new TickAggregationEventPublisher(supervisor, policy: new(), timeProvider: time);
        await publisher.StartAsync();
        try
        {
            await publisher.PublishAsync(first);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await publisher.PublishAsync(old);
            time.Advance(TimeSpan.FromSeconds(6));
        }
        finally { release.TrySetResult(); }
        await Until(() => publisher.GetSnapshot().CanRecover);
        Assert.Equal(RealtimeTickPublisherFailure.QueueExpired, publisher.GetSnapshot().Failure);
        Assert.Equal(1, publisher.GetSnapshot().Expired);
        await producer.DidNotReceive().SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
            old.Subject, old, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Retired_generation_releases_queue_capacity_without_harming_current_generation()
    {
        var (supervisor, producer) = Setup();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var generation = new CancellationTokenSource();
        var first = Price();
        var old = Price();
        producer.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
            first.Subject, first, Arg.Any<CancellationToken>()).Returns(call =>
            { entered.TrySetResult(); return new ValueTask(Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>())); });
        await using var publisher = new TickAggregationEventPublisher(supervisor, policy: new() { Capacity = 1 });
        await publisher.StartAsync();
        await publisher.PublishAsync(first, generation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await publisher.PublishAsync(old, generation.Token);
        await generation.CancelAsync();
        await publisher.PublishAsync(Price());
        await Until(() => publisher.GetSnapshot().Published == 1);
        Assert.Equal(2, publisher.GetSnapshot().GenerationCanceled);
        Assert.False(publisher.GetSnapshot().Faulted);
        await producer.DidNotReceive().SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
            old.Subject, old, Arg.Any<CancellationToken>());
    }

    static (IActorSupervisor, IActorProducer) Setup()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var producer = Substitute.For<IActorProducer>();
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(producer);
        return (supervisor, producer);
    }

    static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }

    sealed class QueueTime : TimeProvider
    {
        long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref timestamp);
        public void Advance(TimeSpan value) => Interlocked.Add(ref timestamp, value.Ticks);
    }

    [Fact]
    public async Task Stage3_queue_saturation_rejects_immediately_instead_of_growing_or_blocking()
    {
        var producer = Substitute.For<IActorProducer>();
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(producer);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        producer.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
            Arg.Any<ActorSubject>(), Arg.Any<FuturesMarketPriceUpdatedRealtimeEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => { entered.TrySetResult(); return new ValueTask(release.Task); });
        await using var publisher = new TickAggregationEventPublisher(supervisor,
            policy: new RealtimeTickPublisherPolicy { Capacity = 1, SendTimeout = TimeSpan.FromSeconds(5) });
        await publisher.StartAsync();
        try
        {
            await publisher.PublishAsync(Price());
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await publisher.PublishAsync(Price());
            await Assert.ThrowsAsync<RealtimeTickPublisherSaturatedException>(async () =>
                await publisher.PublishAsync(Price()).AsTask().WaitAsync(TimeSpan.FromSeconds(1)));
        }
        finally { release.TrySetResult(); }
        await publisher.StopAsync();
    }

    static FuturesMarketPriceUpdatedRealtimeEvent Price() => new()
    {
        Id = Guid.NewGuid(),
        Price = new FuturesMarketPriceSnapshot("ES20261218", 42, 1, AssetTypeId.Futures,
            new DateOnly(2026, 9, 4), null, null)
    };
}
