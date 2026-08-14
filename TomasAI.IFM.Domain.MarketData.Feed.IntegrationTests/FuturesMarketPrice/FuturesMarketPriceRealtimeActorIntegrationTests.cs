using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Net;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesMarketPrice.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.FuturesMarketPrice;

[Trait("Category", "Integration")]
public sealed class FuturesMarketPriceRealtimeActorIntegrationTests
{
    static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    readonly string _url =
        Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";

    [Fact]
    public async Task CoreNatsPublication_ReachesAndExecutesPrimaryRealtimeActor()
    {
        var received = new TaskCompletionSource<FuturesMarketPriceUpdatedRealtimeEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queues = Substitute.For<IActorThreadQueues>();
        var mailbox = Substitute.For<IActorMailbox>();
        mailbox.ThreadQueues.Returns(queues);
        var producer = Substitute.For<IActorProducer>();
        producer.StartAsync(Arg.Any<ActorMailboxId>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);
        producer.StopAsync().Returns(ValueTask.CompletedTask);
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.CreateMailbox(Arg.Any<ActorMailboxId>()).Returns(mailbox);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(producer);
        var actor = new FuturesMarketPriceRealtimeActor(
            supervisor,
            Substitute.For<ILogger<FuturesMarketPriceRealtimeActor>>());
        supervisor.ActorExists(actor.Id).Returns(true);
        supervisor.GetRealtimeRoutes(Arg.Any<ActorTypeId>())
            .Returns(System.Collections.Immutable.ImmutableHashSet<ActorMailboxId>.Empty);
        supervisor.Children.Returns(
            new Dictionary<ActorMailboxId, IActor> { [actor.Id] = actor });
        queues.TryAdmitAsync(
                Arg.Any<IActorMessage>(),
                Arg.Any<ActorSubject>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => AdmitAsync(actor, received, callInfo));
        var consumer = new NatsActorConsumer(
            new NatsConsumerOptions
            {
                Url = _url,
                DispatcherCount = 1,
                DispatcherCapacity = 16,
                SubscriptionCapacity = 16,
                FireAndForgetTraffic = new Dictionary<ActorType, CoreNatsTrafficClass>
                {
                    [ActorType.Realtime] = CoreNatsTrafficClass.Optional
                }
            },
            Substitute.For<ILogger>());
        var publisher = new NatsActorProducer(
            new NatsProducerOptions { Url = _url },
            Substitute.For<ILogger>());
        var @event = CreateEvent();

        try
        {
            await actor.StartAsync(supervisor);
            await consumer.StartAsync(
                supervisor,
                ActorType.Realtime,
                $"futures-market-price-{Guid.NewGuid():N}");
            await Task.Delay(250);
            await publisher.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
                @event.Subject,
                @event);

            var actual = await received.Task.WaitAsync(TestTimeout);

            actual.Should().BeEquivalentTo(@event);
            await queues.Received(1).TryAdmitAsync(
                Arg.Any<IActorMessage>(),
                @event.Subject,
                Arg.Any<CancellationToken>());
        }
        finally
        {
            await publisher.StopAsync();
            await consumer.StopAsync();
            await actor.StopAsync();
        }
    }

    static async ValueTask<ActorAdmissionResult> AdmitAsync(
        FuturesMarketPriceRealtimeActor actor,
        TaskCompletionSource<FuturesMarketPriceUpdatedRealtimeEvent> received,
        NSubstitute.Core.CallInfo callInfo)
    {
        var message = callInfo.Arg<IActorMessage>();
        var subject = callInfo.Arg<ActorSubject>();
        await actor.HandleMessageAsync(message, subject.ThreadId).ConfigureAwait(false);
        received.TrySetResult(message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!);
        return ActorAdmissionResult.AcceptedResult;
    }

    static FuturesMarketPriceUpdatedRealtimeEvent CreateEvent()
    {
        var valueDate = new DateOnly(2026, 8, 14);
        var entityId = new TickDataEntityId("ESZ26", valueDate, AssetTypeId.Futures);
        var timestamp = DateTimeOffset.UtcNow;
        return new FuturesMarketPriceUpdatedRealtimeEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesMarketPriceUpdatedRealtimeEvent.Actor,
                FuturesMarketPriceUpdatedRealtimeEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = Guid.NewGuid(),
            AggregateId = entityId.Format(),
            EventSource = "integration-test",
            ReceivedOn = timestamp.UtcDateTime,
            Price = new FuturesMarketPriceSnapshot(
                entityId.ContractId,
                42,
                7,
                entityId.AssetTypeId,
                entityId.ValueDate,
                new FuturesMarketQuoteSnapshot(
                    5450.00m, 10, 5450.50m, 12, 1, 1, 100, timestamp, timestamp),
                new FuturesMarketTradeSnapshot(
                    5450.25m, 5, 101, timestamp, timestamp))
        };
    }

}
