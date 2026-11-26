using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.RealtimeDelivery;

[Collection("MarketPriceDeliverySafety")]
public sealed class TickStorageDeliverySafetyTests
{
    [Fact]
    public async Task Tick_storage_false_projection_is_failed_settlement()
    {
        // Required projection refusal must fail delivery even without a thrown projector exception.
        var id = new ActorMailboxId(ActorType.Realtime, TickAggregationRealtimeActor.ActorName);
        var context = Substitute.For<ITickAggregationRealtimeContext>(); context.ActorId.Returns(id); context.Logger.Returns(NullLogger<TickAggregationRealtimeActor>.Instance);
        var projector = Substitute.For<IRealtimeProjector<TickAggregationRealtimeActor>>(); context.Projector.Returns(projector);
        projector.ProcessRealtimeEventAsync(Arg.Any<TomasAI.IFM.Shared.EventSourcing.IEvent>()).Returns(ValueTask.FromResult(false));
        var supervisor = Substitute.For<IActorSupervisor>(); var container = Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_ => new ActorThreadQueueV2(16)); supervisor.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        await using var pool = new ActorThreadPoolV2(supervisor, NullLogger.Instance); pool.Initialize(1); supervisor.ThreadPool.Returns(pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call => pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(), Arg.Any<CancellationToken>()).Returns(call => pool.GetThreadAsync(call.Arg<ActorThreadId>(), call.Arg<CancellationToken>()));
        var mailbox = new ActorMailbox(supervisor, id); supervisor.CreateMailbox(id).Returns(mailbox);
        var actor = new TickAggregationRealtimeActor(context); supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [id] = actor });
        var source = new FuturesTickTradeDataChangedEvent
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), AssetTypeId = AssetTypeId.Futures,
            EntityId = new("ES-STORAGE-SAFETY", new(2026, 8, 25), AssetTypeId.Futures),
            Subject = new(ActorType.Realtime, id.Name, FuturesTickTradeDataChangedEvent.Verb, "same"),
            TickDataId = new("ES-STORAGE-SAFETY", new(2026, 8, 25), 1, new(2026, 8, 25, 14, 30, 0, DateTimeKind.Utc)),
            TradeData = new(99, 1787668200000000000, 1787668200000000000, 0, 5450000000000, 5450, 1, 1, 2, 0)
        };
        var message = Substitute.For<IActorMessage, IActorDeliveryCompletion>(); message.Subject.Returns(source.Subject); message.AsEvent<FuturesTickTradeDataChangedEvent>().Returns(source);
        var settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ((IActorDeliveryCompletion)message).CompleteDeliveryAsync(Arg.Any<bool>()).Returns(call => { settled.TrySetResult(call.Arg<bool>()); return ValueTask.CompletedTask; });
        try
        {
            await actor.StartAsync(supervisor); Assert.True(await mailbox.ThreadQueues.WriteAsync(message));
            Assert.False(await settled.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            await projector.Received(1).ProcessRealtimeEventAsync(Arg.Any<TomasAI.IFM.Shared.EventSourcing.IEvent>());
        }
        finally { await actor.StopAsync(); }
    }
}
