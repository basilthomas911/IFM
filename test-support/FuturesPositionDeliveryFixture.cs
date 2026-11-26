using TomasAI.IFM.Domain.Trade.Persistence.Realtime;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Realtime;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.TestSupport;

/// <summary>Actual position actor/scheduler; Function calculation/commit and exit-command acceptance are substituted.</summary>
public sealed class FuturesPositionDeliveryFixture : IAsyncDisposable
{
    public static readonly DateTime At = new(2026, 8, 25, 14, 30, 0, DateTimeKind.Utc);
    public readonly PositionRealtimeIntents PositionIntents;
    public readonly IFuturesTradePositionRealtimeContext Context = Substitute.For<IFuturesTradePositionRealtimeContext>();
    public readonly ActorThreadPoolV2 Pool;
    public readonly ActorMailbox Queue;
    public readonly IActor Actor;
    readonly IActorSupervisor supervisor;
    public readonly Clock Time = new();
    public readonly DailyFuturesExitIntents Intents;
    public StrategyPositionSnapshot? RecoveryPosition;
    public Action? BeforeExit, AfterExit;
    public bool RejectOwner, WrongOwnerReply;
    public TradePlanParameters Parameters = new();
    public string Failure = "none";
    public bool RequiresExit = true;
    public TaskCompletionSource? Pause;
    public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly List<UpdateFuturesTradePlanCommand> Requests = [];
    public readonly Dictionary<Guid, FuturesTradePlanUpdatedEvent> Receipts = [];
    public readonly List<StartFuturesExitPositionWorkflowCommand> ExitAttempts = [], AcceptedExits = [];
    public Func<UpdateFuturesTradePlanCommand, FuturesTradePlanUpdatedEvent, Task<FuturesTradePlanUpdatedEvent>>? Commit;
    public Func<StartFuturesExitPositionWorkflowCommand, Task>? Accept;

    public FuturesPositionDeliveryFixture(bool metrics, Func<IFuturesTradePositionRealtimeContext, IActor>? factory = null, string? intentDirectory = null, string? positionIntentDirectory = null)
    {
        PositionIntents=new(positionIntentDirectory);
        Intents = new(intentDirectory); Context.ExitIntents.Returns(Intents);
        Context.ReadDailyExitRecoveryPositionAsync(Arg.Any<StrategyPositionId>()).Returns(_ => ValueTask.FromResult(RecoveryPosition));
        Context.RequestAsync<StartFuturesExitPositionWorkflowCommand, ExitPositionWorkflowId>(Arg.Any<StartFuturesExitPositionWorkflowCommand>())
            .Returns(call => RequestExit(call.Arg<StartFuturesExitPositionWorkflowCommand>()));
        Context.PositionIntents.Returns(PositionIntents);
        var id = new ActorMailboxId(ActorType.Realtime, FuturesTradePositionRealtimeActor.ActorName);
        Context.ActorId.Returns(id); Context.Logger.Returns(NullLogger<FuturesTradePositionRealtimeActor>.Instance);
        Context.TimeProvider.Returns(Time); Context.Parameters.Returns(_ => Parameters);
        Context.RequestFunctionAsync<UpdateFuturesTradePlanCommand, FuturesTradePlanId, FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>>(Arg.Any<UpdateFuturesTradePlanCommand>())
            .Returns(call => Plan(call.Arg<UpdateFuturesTradePlanCommand>()));
        Context.SendAsync<StartFuturesExitPositionWorkflowCommand, ExitPositionWorkflowId>(Arg.Any<StartFuturesExitPositionWorkflowCommand>(), Arg.Any<ExitPositionWorkflowId>())
            .Returns(call => Send(call.Arg<StartFuturesExitPositionWorkflowCommand>()));
        supervisor = Substitute.For<IActorSupervisor>(); var container = Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_ => new ActorThreadQueueV2(16)); supervisor.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        Pool = new(supervisor, NullLogger.Instance); Pool.Initialize(1); supervisor.ThreadPool.Returns(Pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call => Pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(), Arg.Any<CancellationToken>()).Returns(call => Pool.GetThreadAsync(call.Arg<ActorThreadId>(), call.Arg<CancellationToken>()));
        Queue = new(supervisor, id); var mailbox = Substitute.For<IActorMailbox>();
        mailbox.ThreadQueues.Returns(Queue.ThreadQueues); mailbox.Metrics.Returns((IActorMetricsStore)null!);
        supervisor.CreateMailbox(id).Returns(metrics ? Queue : mailbox);
        Actor = factory?.Invoke(Context) ?? new FuturesTradePositionRealtimeActor(Context);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [id] = Actor });
    }
    async ValueTask<ServiceResult<FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>>> Plan(UpdateFuturesTradePlanCommand command)
    {
        Requests.Add(command); Entered.TrySetResult(); if (Pause is not null) await Pause.Task;
        if (Failure == "throw") throw new IOException("Injected required plan transport failure.");
        if (Failure == "terminal") return new ServiceOk<FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>>(
            FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>.Fail(new() { CommandId = command.CommandId, EntityId = command.EntityId, ErrorData = "INJECTED_FAILURE", ErrorMessage = "Failed terminal" }));
        var completed = new FuturesTradePlanUpdatedEvent
        {
            Id = command.CommandId, CommandId = command.CommandId, EntityId = command.EntityId, SourceEventId = command.SourceEventId,
            ReceivedOn = command.Position.AsOfUtc, RequestFingerprint = command.Fingerprint(),
            Plan = new() { Position = command.Position, ValueDate = command.EntityId.ValueDate, PlanRevision = command.Position.PositionSequence,
                State = RequiresExit ? TradePlanState.ExitRequired : TradePlanState.Normal, Action = RequiresExit ? TradePlanAction.ExitAtMarket : TradePlanAction.Monitor, RequiresExit = RequiresExit, ReasonCode = "test", CalculatedAtUtc = command.RequestedAtUtc }
        };
        if (Commit is not null) completed = await Commit(command, completed);
        else if (Receipts.TryGetValue(command.CommandId, out var original))
        {
            if (original.RequestFingerprint != completed.RequestFingerprint) throw new InvalidDataException("Same plan ID with different parameters.");
            completed = original;
        }
        else Receipts.Add(command.CommandId, completed);
        if (Failure == "lost-reply") throw new IOException("Injected loss after plan receipt commit.");
        return new ServiceOk<FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>>(
            FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>.Complete(completed));
    }
    async ValueTask<ServiceResult<GuidResult>> RequestExit(StartFuturesExitPositionWorkflowCommand command)
    {
        if (RejectOwner) return new ServiceResult<GuidResult>(false, 1, "Injected owner rejection", null);
        await Send(command);
        return new ServiceOk<GuidResult>(new(WrongOwnerReply ? Guid.NewGuid() : command.CommandId));
    }
    async ValueTask Send(StartFuturesExitPositionWorkflowCommand command)
    {
        ExitAttempts.Add(command); BeforeExit?.Invoke();
        if (Failure == "exit-send") throw new IOException("Injected exit command failure before acceptance.");
        if (Accept is not null) await Accept(command);
        AcceptedExits.Add(command); AfterExit?.Invoke();
        if (Failure == "exit-ambiguous") throw new IOException("Injected exit command failure after acceptance.");
    }
    public static PositionChangedEvent Input(int sequence) => Input("Position", sequence);
    public static PositionChangedEvent Input(string kind, int sequence)
    {
        var position = new StrategyPositionSnapshot { Id = StrategyPositionId.Create(new(1, 1, 1, 1), TradeStrategyKind.FuturesOutright),
            StrategyKind = TradeStrategyKind.FuturesOutright, IsOpen = true, PositionSequence = sequence, AsOfUtc = At };
        var id = new Guid(sequence, 0, 0, new byte[8]);
        if (kind == "Clock") return new FuturesPositionExitClockEvent { Id = id, CommandId = id, EntityId = position.Id, PositionSnapshot = position, ReceivedOn = At,
            Subject = new(ActorType.Realtime, FuturesTradePositionRealtimeActor.ActorName, FuturesPositionExitClockEvent.Verb, position.Id.Format()) };
        return new FuturesPositionChangedEvent { Id = id, CommandId = id, EntityId = position.Id, PositionSnapshot = position, ReceivedOn = At,
            Subject = new(ActorType.Realtime, FuturesTradePositionRealtimeActor.ActorName, PositionChangedEvent.Verb, position.Id.Format()) };
    }
    public static PositionChangedEvent DailyInput(string kind, int sequence, long generation = 7)
    {
        var source = Input(sequence);
        var position = source.PositionSnapshot with { RouteGeneration = generation,
            DailyFuturesEntry = new() { DcTriggerId = new("db83633c-5bcb-4335-81dd-dd1a67662409"), ContractId = "ESU6", ValueDate = new(2026,8,25), DcAtUtc = At, DcPrice = 5500, Threshold = 10 } };
        return kind == "Clock" ? DailyFuturesFlattenService.Create(position, new(2026,8,25,20,0,0,DateTimeKind.Utc))!
            : source with { PositionSnapshot = position };
    }
    public ValueTask Start() => Actor.StartAsync(supervisor);
    public async Task<bool> Deliver(PositionChangedEvent source)
    {
        var message = new Delivery(source);
        if (!await Queue.ThreadQueues.WriteAsync(message)) throw new IOException("Fixture admission failed.");
        await message.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5)); return await message.Settled.Task;
    }
    public void Reset() { Requests.Clear(); Receipts.Clear(); ExitAttempts.Clear(); AcceptedExits.Clear(); Context.ClearReceivedCalls(); }
    public async ValueTask DisposeAsync() { Pause?.TrySetResult(); await Actor.StopAsync(); await Pool.DisposeAsync(); await PositionIntents.DisposeAsync(); Intents.Dispose(); }
    public sealed class Clock : TimeProvider { public DateTimeOffset Now = new(At); public override DateTimeOffset GetUtcNow() => Now; }
    sealed class Delivery(PositionChangedEvent source) : IActorMessage, IActorDeliveryCompletion
    {
        public ActorSubject Subject => source.Subject;
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
