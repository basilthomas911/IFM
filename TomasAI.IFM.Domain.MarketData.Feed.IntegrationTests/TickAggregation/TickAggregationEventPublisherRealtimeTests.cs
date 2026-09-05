using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.TickAggregation;

/// <summary>
/// Verifies the production TickAggregation publisher boundary for normalized realtime prices.
/// </summary>
public sealed class TickAggregationEventPublisherRealtimeTests
{
    [Fact]
    public async Task Market_price_update_uses_primary_actor_core_producer_without_owning_its_lifecycle()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var realtimeProducer = Substitute.For<IActorProducer>();
        var primaryId = new ActorMailboxId(
            ActorType.Realtime,
            FuturesTickTradeDataChangedEvent.Actor);
        supervisor.GetProducer(primaryId).Returns(realtimeProducer);

        await using var publisher = new TickAggregationEventPublisher(supervisor);
        await publisher.StartAsync();
        var @event = CreateEvent();

        await publisher.PublishAsync(@event);
        Assert.True(SpinWait.SpinUntil(
            () => realtimeProducer.ReceivedCalls().Any(),
            TimeSpan.FromSeconds(2)));

        await realtimeProducer.Received(1).SendAsync<
            FuturesMarketPriceUpdatedRealtimeEvent,
            TickDataEntityId>(@event.Subject, @event, CancellationToken.None);
        await realtimeProducer.DidNotReceive().StartAsync(Arg.Any<ActorMailboxId>());

        await publisher.StopAsync();
        await realtimeProducer.DidNotReceive().StartAsync(Arg.Any<ActorMailboxId>());
        await realtimeProducer.DidNotReceive().StopAsync();
    }

    [Fact]
    public async Task Slow_nats_delivery_does_not_backpressure_market_data_ingestion()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var realtimeProducer = Substitute.For<IActorProducer>();
        var primaryId = new ActorMailboxId(
            ActorType.Realtime,
            FuturesTickTradeDataChangedEvent.Actor);
        supervisor.GetProducer(primaryId).Returns(realtimeProducer);
        var releaseDelivery = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        realtimeProducer.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
                Arg.Any<ActorSubject>(),
                Arg.Any<FuturesMarketPriceUpdatedRealtimeEvent>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask(releaseDelivery.Task));

        await using var publisher = new TickAggregationEventPublisher(supervisor, capacity: 2);
        await publisher.StartAsync();
        try
        {
            await publisher.PublishAsync(CreateEvent());
            Assert.True(SpinWait.SpinUntil(
                () => realtimeProducer.ReceivedCalls().Any(),
                TimeSpan.FromSeconds(2)));

            var enqueueBurst = Task.Run(async () =>
            {
                for (var index = 0; index < 2_048; index++)
                    await publisher.PublishAsync(CreateEvent());
            });
            Assert.Same(enqueueBurst, await Task.WhenAny(
                enqueueBurst,
                Task.Delay(TimeSpan.FromSeconds(2))));
            await enqueueBurst;
        }
        finally { releaseDelivery.TrySetResult(); }
        await publisher.StopAsync();
    }

    [Fact]
    public async Task Retiring_a_generation_cancels_queued_sends_without_faulting_current_delivery()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var producer = Substitute.For<IActorProducer>();
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(producer);
        using var generation = new CancellationTokenSource();
        var first = CreateEvent();
        var pending = CreateEvent();
        var unaffected = CreateEvent();
        var deliveryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        producer.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
            first.Subject, first, Arg.Any<CancellationToken>()).Returns(call =>
            {
                deliveryStarted.TrySetResult();
                return new ValueTask(Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>()));
            });

        await using var publisher = new TickAggregationEventPublisher(supervisor);
        await publisher.StartAsync();
        try
        {
            await publisher.PublishAsync(first, generation.Token);
            await deliveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await publisher.PublishAsync(pending, generation.Token);
            await publisher.PublishAsync(unaffected, CancellationToken.None);
        }
        finally { await generation.CancelAsync(); }
        await publisher.StopAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));

        await producer.DidNotReceive().SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
            pending.Subject, pending, Arg.Any<CancellationToken>());
        await producer.Received(1).SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
            unaffected.Subject, unaffected, CancellationToken.None);
    }

    private static FuturesMarketPriceUpdatedRealtimeEvent CreateEvent()
    {
        var valueDate = new DateOnly(2026, 8, 14);
        var entityId = new TickDataEntityId(
            "ESU6",
            valueDate,
            AssetTypeId.Futures);
        return new FuturesMarketPriceUpdatedRealtimeEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesMarketPriceUpdatedRealtimeEvent.Actor,
                FuturesMarketPriceUpdatedRealtimeEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            AggregateId = entityId.Format(),
            EventSource = nameof(TickAggregationEventPublisherRealtimeTests),
            ReceivedOn = DateTime.UtcNow,
            Price = new FuturesMarketPriceSnapshot(
                entityId.ContractId,
                42,
                7,
                AssetTypeId.Futures,
                valueDate,
                null,
                new FuturesMarketTradeSnapshot(
                    6050.25m,
                    3,
                    10,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow))
        };
    }
}
