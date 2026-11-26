using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

/// <summary>Regression for independent failed settlement; no financial mutation or live request.</summary>
public sealed class FuturesRoutingSettlementAuditTests
{
    [Fact]
    public async Task Failed_position_route_is_not_acknowledged_and_retry_is_delivered()
    {
        var id = new ActorMailboxId(ActorType.Realtime, FuturesRealtimeActor.ActorName);
        var context = Substitute.For<IFuturesRealtimeContext>();
        context.RoutingReceipts.Returns(new TomasAI.IFM.Domain.Trade.Futures.Realtime.FuturesRoutingReceipts()); context.ActorId.Returns(id);
        context.Logger.Returns(NullLogger<FuturesRealtimeActor>.Instance);
        var routes = new ContractIdRouteIndex(); context.RouteIndex.Returns(routes);
        var route = new PortfolioFundTradeLeg(1, 1, 1, 1, Guid.NewGuid(), Guid.NewGuid(), TradeStrategyKind.FuturesOutright, 1);
        context.DbFactory.TradeDb.GetOpenPositionRouteSnapshotAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<ICollection<OpenPositionRouteReadModel>>(new[] { new OpenPositionRouteReadModel("ESZ6", route) }));
        var requests = 0; var accepted = 0; var reject = true;
        context.ActorService.SendAsync<ChangeTradeLegDataCommand, StrategyPositionId>(Arg.Any<ChangeTradeLegDataCommand>(), Arg.Any<StrategyPositionId>())
            .Returns(call => { requests++; if (reject) return ValueTask.FromResult<ServiceResult<Guid>>(new ServiceFailed<Guid>(1, "Injected position send rejection")); accepted++; return ValueTask.FromResult<ServiceResult<Guid>>(new ServiceOk<Guid>(call.Arg<ChangeTradeLegDataCommand>().CommandId)); });
        var supervisor = Substitute.For<IActorSupervisor>(); var container = Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_ => new ActorThreadQueueV2(16)); supervisor.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        await using var pool = new ActorThreadPoolV2(supervisor, NullLogger.Instance); pool.Initialize(1); supervisor.ThreadPool.Returns(pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call => pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(), Arg.Any<CancellationToken>()).Returns(call => pool.GetThreadAsync(call.Arg<ActorThreadId>(), call.Arg<CancellationToken>()));
        var real = new ActorMailbox(supervisor, id); var mailbox = Substitute.For<IActorMailbox>(); mailbox.ThreadQueues.Returns(real.ThreadQueues); mailbox.Metrics.Returns((IActorMetricsStore)null!);
        supervisor.CreateMailbox(id).Returns(mailbox); var actor = new FuturesRealtimeActor(context);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [id] = actor });
        var source = new FuturesTickTradeDataChangedEvent { Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), ReceivedOn = new(2026,8,25,14,30,0,DateTimeKind.Utc),
            TickDataId = new("ESZ6",new(2026,8,25),1,new(2026,8,25,14,30,0,DateTimeKind.Utc)),
            TradeData = new FuturesTickTradeData(101,0,0,0,0,5500m,1,0,0,0),
            Subject = new(ActorType.Realtime,id.Name,FuturesTickTradeDataChangedEvent.Verb,"same") };
        async Task<bool> Deliver()
        {
            var message = Substitute.For<IActorMessage,IActorDeliveryCompletion>(); message.Subject.Returns(source.Subject); message.AsEvent<FuturesTickTradeDataChangedEvent>().Returns(source);
            var settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ((IActorDeliveryCompletion)message).CompleteDeliveryAsync(Arg.Any<bool>()).Returns(call => { settled.TrySetResult(call.Arg<bool>()); return ValueTask.CompletedTask; });
            Assert.True(await real.ThreadQueues.WriteAsync(message)); return await settled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        try
        {
            await actor.StartAsync(supervisor);
            // Failed delivery must remain retryable without consuming the source cursor.
            Assert.False(await Deliver()); Assert.Equal(1,requests); Assert.Equal(0,accepted);
            reject=false; Assert.True(await Deliver());
            Assert.Equal(2,requests); Assert.Equal(1,accepted); Assert.Equal(0,routes.DuplicateOrOutOfOrderTicks);
        }
        finally { await actor.StopAsync(); }
    }
}
