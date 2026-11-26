using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

/// <summary>Regression for independent failed settlement; no financial mutation or live request.</summary>
public sealed class FuturesPositionSettlementAuditTests
{
    [Fact]
    public async Task Required_plan_exception_fails_delivery_without_optional_metrics()
    {
        var id = new ActorMailboxId(ActorType.Realtime, FuturesTradePositionRealtimeActor.ActorName);
        var context = Substitute.For<IFuturesTradePositionRealtimeContext>(); context.ActorId.Returns(id);
        context.Logger.Returns(NullLogger<FuturesTradePositionRealtimeActor>.Instance); context.TimeProvider.Returns(TimeProvider.System);
        using var intents = new TomasAI.IFM.Domain.Trade.Futures.Position.Realtime.DailyFuturesExitIntents(); context.ExitIntents.Returns(intents);
        context.Parameters.Returns(new TradePlanParameters());
        var requests = 0;
        context.RequestFunctionAsync<UpdateFuturesTradePlanCommand, FuturesTradePlanId, FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>>(Arg.Any<UpdateFuturesTradePlanCommand>())
            .Returns(_ => { requests++; return ValueTask.FromException<ServiceResult<FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>>>(new IOException("Injected required Trade Plan function transport failure.")); });
        var supervisor = Substitute.For<IActorSupervisor>(); var container = Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_ => new ActorThreadQueueV2(16)); supervisor.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        await using var pool = new ActorThreadPoolV2(supervisor, NullLogger.Instance); pool.Initialize(1); supervisor.ThreadPool.Returns(pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call => pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(), Arg.Any<CancellationToken>()).Returns(call => pool.GetThreadAsync(call.Arg<ActorThreadId>(), call.Arg<CancellationToken>()));
        var real = new ActorMailbox(supervisor, id); var mailbox = Substitute.For<IActorMailbox>(); mailbox.ThreadQueues.Returns(real.ThreadQueues); mailbox.Metrics.Returns((IActorMetricsStore)null!);
        supervisor.CreateMailbox(id).Returns(mailbox); var actor = new FuturesTradePositionRealtimeActor(context);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [id] = actor });
        var position = new StrategyPositionSnapshot { Id = StrategyPositionId.Create(new(1, 1, 1, 1), TradeStrategyKind.FuturesOutright), StrategyKind = TradeStrategyKind.FuturesOutright, IsOpen = true, PositionSequence = 1, AsOfUtc = new(2026, 8, 25, 14, 30, 0, DateTimeKind.Utc) };
        var source = new FuturesPositionChangedEvent { Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = position.Id, State = position,
            Subject = new(ActorType.Realtime, id.Name, PositionChangedEvent.Verb, position.Id.Format()) };
        var message = Substitute.For<IActorMessage, IActorDeliveryCompletion>(); message.Subject.Returns(source.Subject); message.AsEvent<FuturesPositionChangedEvent>().Returns(source);
        var settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ((IActorDeliveryCompletion)message).CompleteDeliveryAsync(Arg.Any<bool>()).Returns(call => { settled.TrySetResult(call.Arg<bool>()); return ValueTask.CompletedTask; });
        try
        {
            await actor.StartAsync(supervisor); if (!await real.ThreadQueues.WriteAsync(message)) throw new IOException("Audit input was not admitted.");
            var succeeded = await settled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(succeeded); Assert.Equal(1, requests);

        }
        finally { await actor.StopAsync(); }
    }
}
