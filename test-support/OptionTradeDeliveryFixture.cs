using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.TestSupport;

/// <summary>Actual option-trade actor and scheduler; contract/cache access and Notify transport are substituted.</summary>
public sealed class OptionTradeDeliveryFixture : IAsyncDisposable
{
    public const string Contract = "ES20260918C5500";
    public static readonly DateOnly Date = new(2026, 8, 25);
    public readonly IFuturesOptionTickDataRealtimeContext Context = Substitute.For<IFuturesOptionTickDataRealtimeContext>();
    public readonly IMarketDataApi Api = Substitute.For<IMarketDataApi>();
    public readonly ActorThreadPoolV2 Pool;
    public readonly ActorMailbox Queue;
    public readonly IActor Actor;
    readonly IActorSupervisor supervisor;
    public bool FailLookup, FailSend, Active = true, Catalog = true, QuoteAvailable = true;
    public TaskCompletionSource? PauseLookup;
    public readonly TaskCompletionSource LookupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly List<OptionTradeTickPriceDataUpdatedEvent> Attempts = [], Accepted = [];
    public Func<OptionTradeTickPriceDataUpdatedEvent, ValueTask>? Publish;
    public OptionTickerPriceSnapshot Snapshot = Price();
    public OptionTradeDeliveryFixture(bool metrics, Func<IFuturesOptionTickDataRealtimeContext, IActor>? factory = null)
    {
        var id = new ActorMailboxId(ActorType.Realtime, FuturesOptionTickDataRealtimeActor.ActorName);
        Context.ActorId.Returns(id); Context.Logger.Returns(NullLogger<FuturesOptionTickDataRealtimeActor>.Instance);
        Context.StatusConsoleWriter.Returns(Substitute.For<IStatusConsoleWriter>()); Context.MarketDataApi.Returns(Api);
        Api.IsTickDataStreamActive(Arg.Any<string>()).Returns(_ => Active);
        Api.GetFuturesOptionContractAsync(Arg.Any<string>()).Returns(_ => Lookup());
        Api.TryGetLastOptionTickPrice(Arg.Any<string>(), out Arg.Any<OptionTickerPriceSnapshot>()).Returns(call => { call[1] = Snapshot; return QuoteAvailable; });
        Context.SendAsync<OptionTradeTickPriceDataUpdatedEvent, FuturesOptionTickEntityId>(Arg.Any<OptionTradeTickPriceDataUpdatedEvent>()).Returns(call => Send(call.Arg<OptionTradeTickPriceDataUpdatedEvent>()));
        supervisor = Substitute.For<IActorSupervisor>(); var container = Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_ => new ActorThreadQueueV2(16)); supervisor.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        Pool = new(supervisor, NullLogger.Instance); Pool.Initialize(1); supervisor.ThreadPool.Returns(Pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call => Pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(), Arg.Any<CancellationToken>()).Returns(call => Pool.GetThreadAsync(call.Arg<ActorThreadId>(), call.Arg<CancellationToken>()));
        Queue = new(supervisor, id);
        var mailbox = Substitute.For<IActorMailbox>(); mailbox.ThreadQueues.Returns(Queue.ThreadQueues); mailbox.Metrics.Returns((IActorMetricsStore)null!);
        supervisor.CreateMailbox(id).Returns(metrics ? Queue : mailbox);
        Actor = factory?.Invoke(Context) ?? new FuturesOptionTickDataRealtimeActor(Context);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [id] = Actor });
    }
    async Task<FuturesOptionContractReadModel?> Lookup()
    {
        LookupStarted.TrySetResult(); if (PauseLookup is not null) await PauseLookup.Task;
        if (FailLookup) throw new IOException("Injected required option lookup failure.");
        return Catalog ? new(Contract, "ES call", "ES", "ESU6 C5500", "FOP", "USD", "CME", "50", new(2026, 9, 18), 5500, "Call") : null;
    }
    async ValueTask Send(OptionTradeTickPriceDataUpdatedEvent source)
    {
        Attempts.Add(source);
        if (FailSend) throw new IOException("Injected option Notify transport failure before acceptance.");
        if (Publish is not null) await Publish(source);
        Accepted.Add(source);
    }
    public static OptionTickerPriceSnapshot Price() => new(new(Contract, 99, 7, AssetTypeId.FuturesOption, Date,
        new(12.25m, 100, 12.75m, 150, 1, 1, 100, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch),
        new(12.5m, 25, 101, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)), null);
    public static FuturesTickTradeDataInsertedEvent Input(int sequence) => new()
    {
        Id = new(sequence, 0, 0, new byte[8]), CommandId = new(sequence, 0, 0, new byte[8]),
        EntityId = new(Contract, Date, AssetTypeId.FuturesOption), AssetTypeId = AssetTypeId.FuturesOption,
        Subject = new(ActorType.Realtime, FuturesTickTradeDataInsertedEvent.Actor, FuturesTickTradeDataInsertedEvent.Verb, "same"),
        TickDataId = new(Contract, Date, sequence, new(2026, 8, 25, 14, 30, sequence, DateTimeKind.Utc)),
        TradeData = new(99, 1787668200000000000 + sequence * 1_000_000_000L, 1787668200000000000 + sequence * 1_000_000_000L, 0, 13_125_000_000, 13.125m, 25, 1, 2, 0),
        ReceivedOn = new(2026, 8, 25, 14, 30, sequence, DateTimeKind.Utc)
    };
    public ValueTask Start() => Actor.StartAsync(supervisor);
    public async Task<bool> Deliver(FuturesTickTradeDataInsertedEvent source)
    {
        var message = new Delivery(source);
        if (!await Queue.ThreadQueues.WriteAsync(message)) throw new IOException("Option fixture admission failed.");
        await message.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5)); return await message.Settled.Task;
    }
    public void Reset() { Attempts.Clear(); Accepted.Clear(); Context.ClearReceivedCalls(); Api.ClearReceivedCalls(); }
    public async ValueTask DisposeAsync() { PauseLookup?.TrySetResult(); await Actor.StopAsync(); await Pool.DisposeAsync(); }
    sealed class Delivery(FuturesTickTradeDataInsertedEvent source) : IActorMessage, IActorDeliveryCompletion
    {
        public ActorSubject Subject => source.Subject with { Name = FuturesOptionTickDataRealtimeActor.ActorName };
        public ActorSubject ReplySubject { get; set; }
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
