using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
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
        var durableProducer = Substitute.For<IJSActorProducer>();
        var realtimeProducer = Substitute.For<IActorProducer>();
        var syntheticId = new ActorMailboxId(
            ActorType.Event,
            TickAggregationEventPublisher.SyntheticProducerName);
        var primaryId = new ActorMailboxId(
            ActorType.Realtime,
            FuturesMarketPriceUpdatedRealtimeEvent.Actor);
        supervisor.GetJSEventProducer(syntheticId).Returns(durableProducer);
        supervisor.GetProducer(primaryId).Returns(realtimeProducer);

        await using var publisher = new TickAggregationEventPublisher(supervisor);
        await publisher.StartAsync();
        var @event = CreateEvent();

        await publisher.PublishAsync(@event);

        await realtimeProducer.Received(1).SendAsync<
            FuturesMarketPriceUpdatedRealtimeEvent,
            TickDataEntityId>(@event.Subject, @event);
        await realtimeProducer.DidNotReceive().StartAsync(Arg.Any<ActorMailboxId>());

        await publisher.StopAsync();
        await durableProducer.Received(1).StartAsync(syntheticId);
        await durableProducer.Received(1).StopAsync();
        await realtimeProducer.DidNotReceive().StopAsync();
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
