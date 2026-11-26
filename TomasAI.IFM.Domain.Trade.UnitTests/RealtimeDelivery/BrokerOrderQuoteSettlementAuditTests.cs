using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Domain.Trade.Order.Broker.Realtime.Actor;
using TomasAI.IFM.Application.TradeBroker.Contracts;
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
public sealed class BrokerOrderQuoteSettlementAuditTests
{
    [Fact]
    public async Task Broker_quote_exception_is_not_acknowledged_when_metrics_are_absent()
    {
        var id = new ActorMailboxId(ActorType.Realtime, BrokerOrderRealtimeActor.ActorName);
        var context = Substitute.For<IBrokerOrderRealtimeContext>();
        context.ActorId.Returns(id);
        context.Logger.Returns(NullLogger<BrokerOrderRealtimeActor>.Instance);
        var requests = 0; var accepted = 0; var reject = true;
        context.TradeBroker.PublishMarketQuoteAsync(Arg.Any<BrokerMarketQuote>(),Arg.Any<CancellationToken>())
            .Returns(_=>{requests++;if(reject)throw new IOException("Injected broker quote processing failure.");accepted++;return ValueTask.FromResult(0);});
        var supervisor = Substitute.For<IActorSupervisor>(); var container = Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_ => new ActorThreadQueueV2(16)); supervisor.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        await using var pool = new ActorThreadPoolV2(supervisor, NullLogger.Instance); pool.Initialize(1); supervisor.ThreadPool.Returns(pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call => pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(), Arg.Any<CancellationToken>()).Returns(call => pool.GetThreadAsync(call.Arg<ActorThreadId>(), call.Arg<CancellationToken>()));
        var real = new ActorMailbox(supervisor, id); var mailbox = Substitute.For<IActorMailbox>(); mailbox.ThreadQueues.Returns(real.ThreadQueues); mailbox.Metrics.Returns((IActorMetricsStore)null!);
        supervisor.CreateMailbox(id).Returns(mailbox); var actor = new BrokerOrderRealtimeActor(context);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [id] = actor });
        var at=new DateTime(2026,8,25,14,30,0,DateTimeKind.Utc);var ns=(at.Ticks-DateTime.UnixEpoch.Ticks)*100;
        var source=new FuturesTickQuoteDataChangedEvent
        {
            Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),ReceivedOn=at,DefinitionDate=new(2026,8,25),
            TickDataId=new("ESZ6",new(2026,8,25),1,at),
            QuoteData=new FuturesTickQuoteDataSegment([new FuturesTickQuoteData(1,ns,ns,0,0,5500m,1,1,0,5500.25m,1,1)],1),
            Subject=new(ActorType.Realtime,id.Name,FuturesTickQuoteDataChangedEvent.Verb,"same")
        };
        async Task<bool> Deliver()
        {
            var message = Substitute.For<IActorMessage,IActorDeliveryCompletion>(); message.Subject.Returns(source.Subject); message.AsEvent<FuturesTickQuoteDataChangedEvent>().Returns(source);
            var settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ((IActorDeliveryCompletion)message).CompleteDeliveryAsync(Arg.Any<bool>()).Returns(call => { settled.TrySetResult(call.Arg<bool>()); return ValueTask.CompletedTask; });
            Assert.True(await real.ThreadQueues.WriteAsync(message)); return await settled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        try
        {
            await actor.StartAsync(supervisor);
            // A required broker failure must escape independently of optional metrics.
            Assert.False(await Deliver()); Assert.Equal(1,requests); Assert.Equal(0,accepted);
            reject=false; Assert.True(await Deliver());
            Assert.Equal(2,requests); Assert.Equal(1,accepted);
        }
        finally { await actor.StopAsync(); }
    }
}
