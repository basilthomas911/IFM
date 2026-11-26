using IronCondorTradePlanId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Regression for independent failed settlement; no financial mutation or live request.</summary>
public static class PositionDeliveryAudit
{
    public static async Task Verify()
    {
        var id = new ActorMailboxId(ActorType.Realtime, IronCondorTradePositionRealtimeActor.ActorName);
        var context = Substitute.For<IIronCondorTradePositionRealtimeContext>(); context.ActorId.Returns(id);
        context.Logger.Returns(NullLogger<IronCondorTradePositionRealtimeActor>.Instance); context.TimeProvider.Returns(TimeProvider.System);
        context.IsClosingAsync(Arg.Any<StrategyPositionId>()).Returns(ValueTask.FromResult(false));
        context.CaptureMonitoringInputs(Arg.Any<StrategyPositionSnapshot>(), Arg.Any<DateOnly>(), Arg.Any<DateTime>()).Returns(new IronCondorTradePlanInputs());
        using var captures = new TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.IronCondorPositionRequests(); context.Requests.Returns(captures);
        var requests = 0;
        context.RequestFunctionAsync<UpdateIronCondorTradePlanCommand, IronCondorTradePlanId, FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>(Arg.Any<UpdateIronCondorTradePlanCommand>())
            .Returns(_ => { requests++; return ValueTask.FromException<ServiceResult<FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>>(new IOException("Injected required Trade Plan function transport failure.")); });
        var supervisor = Substitute.For<IActorSupervisor>(); var container = Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_ => new ActorThreadQueueV2(16)); supervisor.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        await using var pool = new ActorThreadPoolV2(supervisor, NullLogger.Instance); pool.Initialize(1); supervisor.ThreadPool.Returns(pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call => pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(), Arg.Any<CancellationToken>()).Returns(call => pool.GetThreadAsync(call.Arg<ActorThreadId>(), call.Arg<CancellationToken>()));
        var real = new ActorMailbox(supervisor, id); var mailbox = Substitute.For<IActorMailbox>(); mailbox.ThreadQueues.Returns(real.ThreadQueues); mailbox.Metrics.Returns((IActorMetricsStore)null!);
        supervisor.CreateMailbox(id).Returns(mailbox); var actor = new IronCondorTradePositionRealtimeActor(context);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [id] = actor });
        var position = new StrategyPositionSnapshot { Id = StrategyPositionId.Create(new(1, 1, 1, 1), TradeStrategyKind.IronCondor), StrategyKind = TradeStrategyKind.IronCondor, IsOpen = true, PositionSequence = 1, AsOfUtc = new(2026, 8, 25, 14, 30, 0, DateTimeKind.Utc) };
        var source = new IronCondorPositionChangedEvent { Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = position.Id, PositionSnapshot = position,
            Subject = new(ActorType.Realtime, id.Name, PositionChangedEvent.Verb, position.Id.Format()) };
        var message = Substitute.For<IActorMessage, IActorDeliveryCompletion>(); message.Subject.Returns(source.Subject); message.AsEvent<IronCondorPositionChangedEvent>().Returns(source);
        var settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ((IActorDeliveryCompletion)message).CompleteDeliveryAsync(Arg.Any<bool>()).Returns(call => { settled.TrySetResult(call.Arg<bool>()); return ValueTask.CompletedTask; });
        try
        {
            await actor.StartAsync(supervisor); if (!await real.ThreadQueues.WriteAsync(message)) throw new IOException("Audit input was not admitted.");
            var succeeded = await settled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (succeeded || requests != 1) throw new InvalidDataException($"Expected failed settlement was not reproduced: settled={succeeded}, requests={requests}.");
            Console.WriteLine("PASS: IronCondorTradePositionRealtime fails settlement after its required Trade Plan Function throws without metrics. One required request attempted; no financial mutation.");
        }
        finally { await actor.StopAsync(); }
    }
}
