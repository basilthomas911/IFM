using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.TestSupport;

/// <summary>Actual tick actor/scheduler, substituted projector with optional real persistence callback.</summary>
public sealed class TickStorageDeliveryFixture : IAsyncDisposable
{
    public static readonly DateOnly Date = new(2026, 8, 25);
    public static readonly Guid Generation = new("2cb4c362-11a1-4787-a81b-a9e57a958a00");
    public readonly ITickAggregationRealtimeContext Context = Substitute.For<ITickAggregationRealtimeContext>();
    public readonly ActorThreadPoolV2 Pool;
    public readonly ActorMailbox Queue;
    public readonly IActor Actor;
    readonly IActorSupervisor supervisor;
    public string Failure = "none";
    public TaskCompletionSource? Pause;
    public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly List<long> Attempts = [], Committed = [];
    public readonly List<byte[]> Payloads = [];
    public readonly List<Guid> SourceGenerations = [];
    public Func<IEvent, Task>? Persist;
    public TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(5);
    public TickStorageDeliveryFixture(bool metrics, Func<ITickAggregationRealtimeContext, IActor>? factory = null)
    {
        var id = new ActorMailboxId(ActorType.Realtime, TickAggregationRealtimeActor.ActorName);
        Context.ActorId.Returns(id); Context.Logger.Returns(NullLogger<TickAggregationRealtimeActor>.Instance);
        var projector = Substitute.For<IRealtimeProjector<TickAggregationRealtimeActor>>(); Context.Projector.Returns(projector);
        projector.ProcessRealtimeEventAsync(Arg.Any<IEvent>()).Returns(call => Project(call.Arg<IEvent>()));
        supervisor = Substitute.For<IActorSupervisor>(); var container = Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_ => new ActorThreadQueueV2(16)); supervisor.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        Pool = new(supervisor, NullLogger.Instance); Pool.Initialize(1); supervisor.ThreadPool.Returns(Pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call => Pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(), Arg.Any<CancellationToken>()).Returns(call => Pool.GetThreadAsync(call.Arg<ActorThreadId>(), call.Arg<CancellationToken>()));
        Queue = new(supervisor, id); var mailbox = Substitute.For<IActorMailbox>();
        mailbox.ThreadQueues.Returns(Queue.ThreadQueues); mailbox.Metrics.Returns((IActorMetricsStore)null!);
        supervisor.CreateMailbox(id).Returns(metrics ? Queue : mailbox);
        Actor = factory?.Invoke(Context) ?? new TickAggregationRealtimeActor(Context);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [id] = Actor });
    }
    async ValueTask<bool> Project(IEvent source)
    {
        var sequence = source is FuturesTickTradeDataInsertedEvent trade ? trade.TickDataId.SequenceId : ((FuturesTickQuoteDataInsertedEvent)source).TickDataId.SequenceId;
        Attempts.Add(sequence); Entered.TrySetResult(); if (Pause is not null) await Pause.Task;
        SourceGenerations.Add(source is FuturesTickTradeDataInsertedEvent tradeGeneration ? tradeGeneration.SourceGenerationId : ((FuturesTickQuoteDataInsertedEvent)source).SourceGenerationId);
        if (Failure == "refused") return false;
        if (Failure == "throw") throw new IOException("Injected tick projection failure.");
        if (Persist is not null) await Persist(source);
        Payloads.Add(source switch
        {
            FuturesTickTradeDataInsertedEvent t => MessagePackSerializer.Serialize(t),
            FuturesTickQuoteDataInsertedEvent q => MessagePackSerializer.Serialize(q),
            _ => throw new InvalidDataException("Unexpected tick projection.")
        });
        Committed.Add(sequence);
        if (Failure == "lost-reply") throw new IOException("Injected reply loss after tick persistence.");
        return true;
    }
    public static IEvent Input(string kind, int sequence, string contract = "ES-TICK-SETTLEMENT")
    {
        var id = new Guid(sequence, 0, 0, new byte[8]); var time = new DateTime(2026, 8, 25, 14, 30, sequence, DateTimeKind.Utc);
        var entity = new TickDataEntityId(contract, Date, AssetTypeId.Futures); var tickId = new TickDataId(contract, Date, sequence, time);
        if (kind == "Trade") return new FuturesTickTradeDataChangedEvent
        {
            Id = id, CommandId = id, EntityId = entity, TickDataId = tickId, ReceivedOn = time, AssetTypeId = AssetTypeId.Futures,
            Subject = new(ActorType.Realtime, TickAggregationRealtimeActor.ActorName, FuturesTickTradeDataChangedEvent.Verb, "same"),
            Dataset = "GLBX.MDP3", SourceDataset = "GLBX.MDP3", SourceGenerationId = Generation,
            TradeData = new((uint)sequence, 1787668200000000000 + sequence * 1_000_000_000L, 1787668200000000000 + sequence * 1_000_000_000L, 0, 5450250000000, 5450.25m, 3, 1, 2, 0)
        };
        return new FuturesTickQuoteDataChangedEvent
        {
            Id = id, CommandId = id, EntityId = entity, TickDataId = tickId, ReceivedOn = time, AssetTypeId = AssetTypeId.Futures,
            Subject = new(ActorType.Realtime, TickAggregationRealtimeActor.ActorName, FuturesTickQuoteDataChangedEvent.Verb, "same"),
            Dataset = "GLBX.MDP3", SourceDataset = "GLBX.MDP3", SourceGenerationId = Generation,
            EmissionReason = QuoteEmissionReason.BufferFull, QuoteCount = 2,
            QuoteData = new([new((uint)sequence, 1787668200000000000, 1787668200000000001, 0, 5450000000000, 5450, 10, 1, 5450250000000, 5450.25m, 20, 1),
                new((uint)(sequence + 10), 1787668201000000000, 1787668201000000001, 0, 5450250000000, 5450.25m, 11, 1, 5450500000000, 5450.5m, 21, 1)], 2)
        };
    }
    public static byte[] Encode(IEvent source) => source switch
    {
        FuturesTickTradeDataChangedEvent t => MessagePackSerializer.Serialize(t),
        FuturesTickQuoteDataChangedEvent q => MessagePackSerializer.Serialize(q),
        _ => throw new InvalidDataException("Unexpected tick source.")
    };
    public static IEvent Decode(string kind, byte[] bytes) => kind == "Trade" ? MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(bytes) : MessagePackSerializer.Deserialize<FuturesTickQuoteDataChangedEvent>(bytes);
    public ValueTask Start() => Actor.StartAsync(supervisor);
    public async Task<bool> Deliver(IEvent source)
    {
        var message = new Delivery(source);
        if (!await Queue.ThreadQueues.WriteAsync(message)) { if (source is FuturesTickQuoteDataChangedEvent quote) quote.QuoteData.Dispose(); throw new IOException("Tick fixture admission failed."); }
        await message.Disposed.Task.WaitAsync(DeliveryTimeout); return await message.Settled.Task;
    }
    public void Reset() { Attempts.Clear(); Committed.Clear(); Payloads.Clear(); SourceGenerations.Clear(); Context.Projector.ClearReceivedCalls(); Context.ClearReceivedCalls(); }
    public async ValueTask DisposeAsync() { Pause?.TrySetResult(); await Actor.StopAsync(); await Pool.DisposeAsync(); }
    sealed class Delivery(IEvent source) : IActorMessage, IActorDeliveryCompletion
    {
        public ActorSubject Subject => source.Subject;
        public ActorSubject ReplySubject { get; set; }
        public string? SourceDataset => "GLBX.MDP3";
        public Guid SourceGenerationId => Generation;
        public readonly TaskCompletionSource<bool> Settled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => source as TEvent;
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => null;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => null;
        public ValueTask ReplyAsync<TResult>(TResult value) where TResult : class => ValueTask.CompletedTask;
        public ValueTask CompleteDeliveryAsync(bool succeeded) { Settled.TrySetResult(succeeded); return ValueTask.CompletedTask; }
        public void ReleasePayload() { }
        public void Dispose() => Disposed.TrySetResult();
    }
}
