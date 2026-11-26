using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Domain.Trade.Order.Broker.Realtime.Actor;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using MessagePack;
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

using TomasAI.IFM.Domain.Trade.Futures.Realtime;
namespace TomasAI.IFM.TestSupport;
/// <summary>Actual broker realtime actor/scheduler; broker effects can be substituted or bound to a real emulator.</summary>
public sealed class BrokerOrderDeliveryFixture : IAsyncDisposable
{
    public readonly IBrokerOrderRealtimeContext Context=Substitute.For<IBrokerOrderRealtimeContext>();
    public readonly List<BrokerMarketQuote> Attempts=[];
    public Func<BrokerMarketQuote,ValueTask<int>>? Publish;
    public string Failure="none";
    public readonly ActorThreadPoolV2 Pool;
    public readonly ActorMailbox Queue;
    public readonly IActor Actor;
    readonly IActorSupervisor supervisor;
    public static readonly DateTime At=new(2026,8,25,14,30,0,DateTimeKind.Utc);
    public BrokerOrderDeliveryFixture(Func<IBrokerOrderRealtimeContext,IActor>? factory=null,bool metrics=false)
    {
        var id=new ActorMailboxId(ActorType.Realtime,BrokerOrderRealtimeActor.ActorName);
        Context.ActorId.Returns(id);Context.Logger.Returns(NullLogger<BrokerOrderRealtimeActor>.Instance);
        Context.TradeBroker.PublishMarketQuoteAsync(Arg.Any<BrokerMarketQuote>(),Arg.Any<CancellationToken>()).Returns(call=>Send(call.Arg<BrokerMarketQuote>()));
        supervisor=Substitute.For<IActorSupervisor>();var container=Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_=>new ActorThreadQueueV2(16));supervisor.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        Pool=new(supervisor,NullLogger.Instance);Pool.Initialize(1);supervisor.ThreadPool.Returns(Pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call=>Pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(),Arg.Any<CancellationToken>()).Returns(call=>Pool.GetThreadAsync(call.Arg<ActorThreadId>(),call.Arg<CancellationToken>()));
        Queue=new(supervisor,id);var mailbox=Substitute.For<IActorMailbox>();mailbox.ThreadQueues.Returns(Queue.ThreadQueues);mailbox.Metrics.Returns((IActorMetricsStore)null!);
        supervisor.CreateMailbox(id).Returns(metrics?Queue:mailbox);Actor=factory?.Invoke(Context)??new BrokerOrderRealtimeActor(Context);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId,IActor>{[id]=Actor});
    }
    async ValueTask<int> Send(BrokerMarketQuote quote)
    {
        Attempts.Add(quote);
        if(Failure=="throw")throw new IOException("Injected failure before broker acceptance.");
        var result=Publish is null?0:await Publish(quote);
        if(Failure=="lost-reply")throw new IOException("Injected failure after broker acceptance.");
        return result;
    }
    public static IEvent Input(string kind,int sequence)
    {
        var time=At.AddMilliseconds(sequence);var ns=(time.Ticks-DateTime.UnixEpoch.Ticks)*100;
        if(kind=="Option")
        {
            var source=FuturesOptionRoutingDeliveryFixture.Input(sequence);
            return source with{Subject=new(ActorType.Realtime,BrokerOrderRealtimeActor.ActorName,FuturesTickTradeDataChangedEvent.Verb,"same")};
        }
        return new FuturesTickQuoteDataChangedEvent
        {
            Id=new Guid(sequence,0,0,new byte[8]),CommandId=new Guid(sequence,0,0,new byte[8]),ReceivedOn=time,DefinitionDate=new(2026,8,25),
            TickDataId=new("ESZ6",new(2026,8,25),sequence,time),
            Subject=new(ActorType.Realtime,BrokerOrderRealtimeActor.ActorName,FuturesTickQuoteDataChangedEvent.Verb,"same"),
            QuoteCount=2,QuoteData=new FuturesTickQuoteDataSegment([
                new FuturesTickQuoteData((uint)(sequence+1000),ns-100,ns-100,0,0,5499m,1,1,0,5499.25m,1,1),
                new FuturesTickQuoteData((uint)sequence,ns,ns,0,0,5500m,2,1,0,5500.25m,3,1)],2)
        };
    }
    public static byte[] Encode(IEvent source)=>source switch
    {FuturesTickQuoteDataChangedEvent quote=>MessagePackSerializer.Serialize(quote),FuturesTickTradeDataChangedEvent option=>MessagePackSerializer.Serialize(option),_=>throw new InvalidDataException("Unsupported source.")};
    public static IEvent Decode(string kind,byte[] bytes)=>kind=="Option"?MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(bytes):MessagePackSerializer.Deserialize<FuturesTickQuoteDataChangedEvent>(bytes);
    public ValueTask Start()=>Actor.StartAsync(supervisor);
    public async Task<bool> Deliver(IEvent source)
    {
        var delivery=new Delivery(source);
        if(!await Queue.ThreadQueues.WriteAsync(delivery))
        {if(source is FuturesTickQuoteDataChangedEvent quote)quote.QuoteData.Dispose();throw new IOException("Fixture queue rejected source.");}
        await delivery.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));return await delivery.Settled.Task;
    }
    public void Reset(){Attempts.Clear();Context.TradeBroker.ClearReceivedCalls();Context.ClearReceivedCalls();supervisor.ClearReceivedCalls();}
    public async ValueTask DisposeAsync(){await Actor.StopAsync();await Pool.DisposeAsync();}
    sealed class Delivery(IEvent source):IActorMessage,IActorDeliveryCompletion
    {
        public ActorSubject Subject=>source.Subject;public ActorSubject ReplySubject{get;set;}
        public readonly TaskCompletionSource<bool> Settled=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Disposed=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TEvent? AsEvent<TEvent>()where TEvent:class,IEvent=>source as TEvent;
        public TCommand? AsCommand<TCommand>()where TCommand:class,ICommand=>null;
        public TQuery? AsQuery<TQuery,TResult>()where TQuery:class,IQuery<TResult>where TResult:class=>null;
        public ValueTask ReplyAsync<TResult>(TResult result)where TResult:class=>ValueTask.CompletedTask;
        public void ReleasePayload(){}
        public void Dispose()=>Disposed.TrySetResult();
        public ValueTask CompleteDeliveryAsync(bool succeeded){Settled.TrySetResult(succeeded);return ValueTask.CompletedTask;}
    }
}
