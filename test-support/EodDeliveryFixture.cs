using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.TestSupport;

/// <summary>Real EOD actor, scheduler and statistics math; query/projector persistence are substituted.</summary>
public sealed class EodDeliveryFixture : IAsyncDisposable
{
    public const string Contract = "ES-EOD-DELIVERY";
    public static readonly DateOnly Date = new(2026, 8, 25);
    public readonly IFuturesEodDataRealtimeContext Context;
    public readonly FuturesEodDataRealtimeActor Actor;
    public readonly ActorThreadPoolV2 Pool;
    public readonly ActorMailbox Mailbox;
    readonly IActorSupervisor supervisor;
    public bool Fail, Throw, ThrowAfterCommit;
    public TaskCompletionSource? PauseQuery, PauseProjection;
    public readonly TaskCompletionSource QueryStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly TaskCompletionSource ProjectionStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly List<FuturesEodDataV2ReadModel> Attempts = [], Committed = [];
    public readonly List<Guid> ProjectionIds = [];
    public FuturesEodDataV2ReadModel Current = Initial();
    public Func<ValueTask<FuturesEodDataV2ReadModel>>? ReadRow;
    public Func<FuturesEodDataV2ReadModel, ValueTask>? WriteRow;
    public EodDeliveryFixture(bool workers)
    {
        Context = Substitute.For<IFuturesEodDataRealtimeContext>();
        Context.Logger.Returns(NullLogger<FuturesEodDataRealtimeActor>.Instance);
        Context.EnableAsyncTradeWorker.Returns(workers); Context.TradeDispatch.Returns(new FuturesEodTradeDispatchState());
        Context.MarketDataApi.Returns(Substitute.For<IMarketDataApi>());
        Context.StatusConsoleWriter.Returns(Substitute.For<IStatusConsoleWriter>());
        var projector = Substitute.For<IRealtimeProjector<FuturesEodDataRealtimeActor>>(); Context.Projector.Returns(projector);
        Context.RequestAsync<FuturesEodDataV2ReadModel, GetFuturesEodDataQuery>(Arg.Any<GetFuturesEodDataQuery>())
            .Returns(_ => Query());
        projector.ProcessRealtimeEventAsync(Arg.Any<IEvent>()).Returns(call => Project(call.Arg<IEvent>()));
        supervisor = Substitute.For<IActorSupervisor>(); var container = Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_ => new ActorThreadQueueV2(16)); supervisor.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        Pool = new(supervisor, NullLogger.Instance); Pool.Initialize(2); supervisor.ThreadPool.Returns(Pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call => Pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(), Arg.Any<CancellationToken>())
            .Returns(call => Pool.GetThreadAsync(call.Arg<ActorThreadId>(), call.Arg<CancellationToken>()));
        var id = new ActorMailboxId(ActorType.Realtime, FuturesEodDataRealtimeActor.ActorName); Context.ActorId.Returns(id);
        Mailbox = new(supervisor, id); supervisor.CreateMailbox(id).Returns(Mailbox); Actor = new(Context);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [id] = Actor });
    }
    async ValueTask<ServiceResult<FuturesEodDataV2ReadModel>> Query()
    {
        QueryStarted.TrySetResult(); if (PauseQuery is not null) await PauseQuery.Task;
        return new ServiceOk<FuturesEodDataV2ReadModel>(ReadRow is null ? Current : await ReadRow());
    }
    async ValueTask<bool> Project(IEvent source)
    {
        var projected = (FuturesEodSessionStatisticsUpdatedEvent)source;
        Attempts.Add(projected.FuturesEodData); ProjectionIds.Add(source.Id); ProjectionStarted.TrySetResult();
        if (PauseProjection is not null) await PauseProjection.Task;
        if (Throw) throw new IOException("Injected EOD projection transport failure.");
        if (Fail) return false;
        if (WriteRow is not null) await WriteRow(projected.FuturesEodData); Current = projected.FuturesEodData;
        Committed.Add(Current);
        if (ThrowAfterCommit) throw new IOException("Injected EOD reply loss after database commit.");
        return true;
    }
    public static FuturesEodDataV2ReadModel Initial() => new()
    { ContractId = Contract, ValueDate = Date, Symbol = "ES", ClosePrice = 100, OpenPrice = 99, HighPrice = 110, LowPrice = 90, Volume = 10 };
    public static FuturesSessionStatisticsUpdatedRealtimeEvent Source(int sequence) => new()
    {
        Id = new(sequence, 0, 0, new byte[8]), CommandId = new(sequence, 0, 0, new byte[8]), EntityId = new(Contract, Date),
        Subject = new(ActorType.Realtime, FuturesSessionStatisticsUpdatedRealtimeEvent.Actor, FuturesSessionStatisticsUpdatedRealtimeEvent.Verb, "same"),
        Statistics = new(Contract, Date, sequence == 1 ? 98 : 97, sequence == 1 ? 110 : 115, 90, (uint)sequence, sequence, sequence == 1 ? 20 : 30, FuturesSessionVolumeQuality.OfficialFinal)
    };
    public ValueTask Start() => Actor.StartAsync(supervisor);
    public async Task<bool> Deliver(FuturesSessionStatisticsUpdatedRealtimeEvent source, string thread = "same")
    {
        var message = new Message(source, thread);
        if (!await Mailbox.ThreadQueues.WriteAsync(message)) throw new IOException("Fixture admission refused.");
        await message.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5)); return await message.Settled.Task;
    }
    public async ValueTask DisposeAsync()
    {
        PauseQuery?.TrySetResult(); PauseProjection?.TrySetResult();
        await Actor.StopAsync(); await Pool.DisposeAsync();
    }
    sealed class Message(FuturesSessionStatisticsUpdatedRealtimeEvent source, string thread) : IActorMessage, IActorDeliveryCompletion
    {
        public ActorSubject Subject => new(ActorType.Realtime, FuturesEodDataRealtimeActor.ActorName, source.Subject.Verb, thread);
        public ActorSubject ReplySubject { get; set; }
        public readonly TaskCompletionSource<bool> Settled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => source as TEvent;
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => null;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => null;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public ValueTask CompleteDeliveryAsync(bool succeeded) { Settled.TrySetResult(succeeded); return ValueTask.CompletedTask; }
        public void ReleasePayload() { }
        public void Dispose() => Disposed.TrySetResult();
    }
}
