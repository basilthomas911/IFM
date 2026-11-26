using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
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

using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
namespace TomasAI.IFM.TestSupport;

/// <summary>Actual router actor, scheduler and index; position command receiver is substituted.</summary>
public sealed class FuturesOptionRoutingDeliveryFixture : IAsyncDisposable
{
    public readonly IFuturesOptionRealtimeContext Context = Substitute.For<IFuturesOptionRealtimeContext>();
    public readonly FuturesOptionRoutingReceipts Journal;
    public readonly ContractIdRouteIndex Routes = new();
    public readonly List<ChangeTradeLegDataCommand> Attempts = [];
    public Func<ChangeTradeLegDataCommand, ValueTask<ServiceResult<Guid>>>? Send;
    public readonly List<TradeRealtimeSnapshotChangedEvent> Publications=[];
    public Func<TradeRealtimeSnapshotChangedEvent,ValueTask>? Publish;
    public readonly TradeRealtimeCacheRegistry? Cache;
    public sealed class Clock:TimeProvider { public DateTimeOffset Now=new(2026,8,25,14,30,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Now; }
    public readonly Clock Time=new();
    public readonly ActorThreadPoolV2 Pool;
    public readonly ActorMailbox Queue;
    public readonly IActor Actor;
    public static readonly DateTime At = new(2026,8,25,14,30,0,DateTimeKind.Utc);
    public static PortfolioFundTradeLeg Route(int n) => new(1,1,1,n,new Guid(n,0,0,new byte[8]),new Guid(n+100,0,(short)n,new byte[8]),n==1?TradeStrategyKind.IronCondor:TradeStrategyKind.VerticalSpread,1);
    public FuturesOptionRoutingDeliveryFixture(string? directory = null, Func<IFuturesOptionRealtimeContext,IActor>? factory = null, int branches = 2, bool withCache = true)
    {
        var id = new ActorMailboxId(ActorType.Realtime,FuturesOptionRealtimeActor.ActorName);
        Journal = new(directory); Context.ActorId.Returns(id); Context.RoutingReceipts.Returns(Journal);
        Context.Logger.Returns(NullLogger<FuturesOptionRealtimeActor>.Instance); Context.RouteIndex.Returns(Routes);
        Context.DbFactory.TradeDb.GetOpenPositionRouteSnapshotAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<ICollection<OpenPositionRouteReadModel>>(Enumerable.Range(1,branches).Select(n=>new OpenPositionRouteReadModel("ESZ6 C5500",Route(n))).ToArray()));
        Context.ActorService.SendAsync<ChangeTradeLegDataCommand,StrategyPositionId>(Arg.Any<ChangeTradeLegDataCommand>(),Arg.Any<StrategyPositionId>())
            .Returns(call => Dispatch(call.Arg<ChangeTradeLegDataCommand>()));
        if(withCache)
        {
            Cache=new(Time);Context.RealtimeCache.Returns(Cache);
            foreach(var n in Enumerable.Range(1,branches))Cache.Register(new EstablishedTradeDefinition
            {
                Id=new(1,1,1,n),StrategyKind=Route(n).TradeType,
                Legs=[new TradeLegDefinition{TradeLegId=Route(n).TradeLegId,ContractId="ESZ6 C5500",PutCall=1,SignedQuantity=-1,CashMultiplier=50}]
            },new(2026,8,25));
        }
        Context.SendAsync<TradeRealtimeSnapshotChangedEvent,TradeEntityId>(Arg.Any<TradeRealtimeSnapshotChangedEvent>())
            .Returns(call=>Notify(call.Arg<TradeRealtimeSnapshotChangedEvent>()));
        var supervisor=Substitute.For<IActorSupervisor>(); var container=Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_=>new ActorThreadQueueV2(16)); supervisor.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        Pool=new(supervisor,NullLogger.Instance); Pool.Initialize(1); supervisor.ThreadPool.Returns(Pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call=>Pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(),Arg.Any<CancellationToken>()).Returns(call=>Pool.GetThreadAsync(call.Arg<ActorThreadId>(),call.Arg<CancellationToken>()));
        Queue=new(supervisor,id); var mailbox=Substitute.For<IActorMailbox>(); mailbox.ThreadQueues.Returns(Queue.ThreadQueues); mailbox.Metrics.Returns((IActorMetricsStore)null!);
        supervisor.CreateMailbox(id).Returns(mailbox); Actor=factory?.Invoke(Context)??new FuturesOptionRealtimeActor(Context);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId,IActor>{[id]=Actor});
        Supervisor=supervisor;
    }
    readonly IActorSupervisor Supervisor;
    async ValueTask<ServiceResult<Guid>> Dispatch(ChangeTradeLegDataCommand command)
    { Attempts.Add(command); return Send is null ? new ServiceOk<Guid>(command.CommandId) : await Send(command); }
    async ValueTask Notify(TradeRealtimeSnapshotChangedEvent publication)
    {Publications.Add(publication);if(Publish is not null)await Publish(publication);}
    public static FuturesTickTradeDataChangedEvent Input(int sequence, string kind="Quote")
    {
        var quote=new FuturesOptionTickDataV2ReadModel
        {
            ContractId="ESZ6 C5500",ValueDate=new(2026,8,25),TickId=sequence,OptionPrice=10,
            BidPrice=9,AskPrice=11,BidSize=2,AskSize=3,ObservedAtUtc=new(At.AddSeconds(sequence)),ValidUntilUtc=new(At.AddSeconds(sequence+5)),
            SourceGeneration=new Guid(124,0,0,new byte[8]),ObservationRevision=sequence,
            IsPricingUpdate=kind=="Pricing",GreeksAvailable=kind=="Pricing",PricingFailure=kind=="Pricing"?null:"CalculationPending",
            CalculatedAtUtc=kind=="Pricing"?new(At.AddSeconds(sequence).AddMilliseconds(1)):default,
            ImpliedVolatility=.2,UnderlyingPrice=5500,Delta=.16,Gamma=.01,Vega=12,Theta=-3,Rho=.1,TheoreticalPrice=10
        };
        return new()
        {
            Id=new Guid(sequence,1,0,new byte[8]),CommandId=new Guid(sequence,2,0,new byte[8]),ReceivedOn=At.AddSeconds(sequence),
            TickDataId=new("ESZ6",new(2026,8,25),sequence,At.AddSeconds(sequence)),
            TradeData=new FuturesTickTradeData((uint)sequence,0,0,0,0,5500m+sequence,1,0,0,0),
            OptionMarketPriceObservation=new("ESZ6",OptionMarketPriceBasis.QuoteMidpoint,quote,sequence,At.AddSeconds(sequence)),
            SourceDataset="GLBX.MDP3",SourceGenerationId=new Guid(123,0,0,new byte[8]),
            Subject=new(ActorType.Realtime,FuturesOptionRealtimeActor.ActorName,FuturesTickTradeDataChangedEvent.Verb,"ESZ6 C5500")
        };
    }
    public ValueTask Start()=>Actor.StartAsync(Supervisor);
    public async Task<bool> Deliver(FuturesTickTradeDataChangedEvent source)
    {
        var message=Substitute.For<IActorMessage,IActorDeliveryCompletion>(); message.Subject.Returns(source.Subject);
        message.SourceDataset.Returns(source.SourceDataset);message.SourceGenerationId.Returns(source.SourceGenerationId);
        message.AsEvent<FuturesTickTradeDataChangedEvent>().Returns(source);
        var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ((IActorDeliveryCompletion)message).CompleteDeliveryAsync(Arg.Any<bool>()).Returns(call=>{completion.TrySetResult(call.Arg<bool>());return ValueTask.CompletedTask;});
        if(!await Queue.ThreadQueues.WriteAsync(message)) throw new IOException("Fixture queue rejected source.");
        return await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
    public void ResetMeasurements(){Attempts.Clear();Publications.Clear();Context.ActorService.ClearReceivedCalls();Context.ClearReceivedCalls();Supervisor.ClearReceivedCalls();}
    public async ValueTask DisposeAsync(){await Actor.StopAsync();await Pool.DisposeAsync();Journal.Dispose();}
}
