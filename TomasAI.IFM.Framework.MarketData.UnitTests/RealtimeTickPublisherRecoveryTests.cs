using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.MarketData.UnitTests;

public sealed class RealtimeTickPublisherRecoveryTests
{
    [Fact]
    public async Task Transient_transport_failure_retries_same_publication_until_success()
    {
        var (supervisor, producer) = Setup();
        var attempts = 0;
        producer.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
                Arg.Any<ActorSubject>(), Arg.Any<FuturesMarketPriceUpdatedRealtimeEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref attempts) < 4
                ? ValueTask.FromException(new IOException("transient"))
                : ValueTask.CompletedTask);
        await using var publisher = new TickAggregationEventPublisher(supervisor, policy: new()
        {
            SendTimeout = TimeSpan.FromSeconds(1),
            InitialRetryDelay = TimeSpan.FromMilliseconds(10),
            MaximumRetryDelay = TimeSpan.FromMilliseconds(20)
        });

        await publisher.StartAsync();
        await publisher.PublishAsync(Price());
        await Until(() => publisher.GetSnapshot().Published == 1);

        var snapshot = publisher.GetSnapshot();
        Assert.Equal(4, attempts);
        Assert.Equal(3, snapshot.Failed);
        Assert.False(snapshot.Faulted);
        Assert.NotNull(snapshot.FirstFailureUtc);
        Assert.Equal(typeof(IOException).FullName, snapshot.LastExceptionType);
    }

    [Fact]
    public async Task Noncooperative_send_requires_reset_after_no_progress_boundary_and_can_restart_after_retirement()
    {
        var (supervisor, producer) = Setup();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        producer.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
                Arg.Any<ActorSubject>(), Arg.Any<FuturesMarketPriceUpdatedRealtimeEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => { entered.TrySetResult(); return new ValueTask(release.Task); });
        await using var publisher = new TickAggregationEventPublisher(supervisor, policy: new()
        {
            SendTimeout = TimeSpan.FromMilliseconds(50),
            CancellationGracePeriod = TimeSpan.FromMilliseconds(20),
            NoProgressResetThreshold = TimeSpan.FromMilliseconds(250)
        });

        await publisher.StartAsync();
        await publisher.PublishAsync(Price());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Until(() => publisher.GetSnapshot().ResetRequired);
        var stalled = publisher.GetSnapshot();
        Assert.Equal(RealtimeTickPublisherFailure.NonCooperativeSend, stalled.Failure);
        Assert.True(stalled.UncontainedSend);
        Assert.NotEmpty(stalled.InFlightEventType);
        Assert.NotEmpty(stalled.InFlightSubject);

        release.TrySetException(new IOException("eventual transport failure"));
        await Until(() => publisher.GetSnapshot().CanRecover);
        Assert.Equal(typeof(IOException).FullName, publisher.GetSnapshot().LastExceptionType);
        await publisher.StartAsync();
        Assert.True(publisher.IsRunning);
    }

    static (IActorSupervisor Supervisor, IActorProducer Producer) Setup()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var producer = Substitute.For<IActorProducer>();
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(producer);
        return (supervisor, producer);
    }

    static FuturesMarketPriceUpdatedRealtimeEvent Price() => new()
    {
        Id = Guid.NewGuid(),
        Price = new FuturesMarketPriceSnapshot("ES20261218", 42, 1, AssetTypeId.Futures,
            new DateOnly(2026, 9, 29), null, null)
    };

    static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }
}
