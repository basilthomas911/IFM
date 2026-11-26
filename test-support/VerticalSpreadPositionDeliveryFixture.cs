using TomasAI.IFM.Domain.Trade.Persistence.Realtime;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.TestSupport;

/// <summary>Actual position actor/scheduler; Function calculation/commit and exit-command acceptance are substituted.</summary>
public sealed class VerticalSpreadPositionDeliveryFixture : IAsyncDisposable
{
    public static readonly DateTime At = new(2026, 8, 25, 14, 30, 0, DateTimeKind.Utc);
    public readonly PositionRealtimeIntents PositionIntents;
    public readonly IVerticalSpreadTradePositionRealtimeContext Context = Substitute.For<IVerticalSpreadTradePositionRealtimeContext>();
    public readonly ActorThreadPoolV2 Pool;
    public readonly ActorMailbox Queue;
    public readonly IActor Actor;
    readonly IActorSupervisor supervisor;
    public readonly Clock Time = new();
    public TradePlanParameters Parameters = new();
    public string Failure = "none";
    public bool RequiresExit = true;
    public TaskCompletionSource? Pause;
    public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly List<UpdateVerticalSpreadTradePlanCommand> Requests = [];
    public readonly Dictionary<Guid, VerticalSpreadTradePlanUpdatedEvent> Receipts = [];
    public readonly List<StartVerticalSpreadExitPositionWorkflowCommand> ExitAttempts = [], AcceptedExits = [];
    public Func<UpdateVerticalSpreadTradePlanCommand, VerticalSpreadTradePlanUpdatedEvent, Task<VerticalSpreadTradePlanUpdatedEvent>>? Commit;
    public Func<StartVerticalSpreadExitPositionWorkflowCommand, Task>? Accept;

    public VerticalSpreadPositionDeliveryFixture(bool metrics, Func<IVerticalSpreadTradePositionRealtimeContext, IActor>? factory = null, string? positionIntentDirectory = null)
    {
        PositionIntents=new(positionIntentDirectory);
        Context.PositionIntents.Returns(PositionIntents);
        var id = new ActorMailboxId(ActorType.Realtime, VerticalSpreadTradePositionRealtimeActor.ActorName);
        Context.ActorId.Returns(id); Context.Logger.Returns(NullLogger<VerticalSpreadTradePositionRealtimeActor>.Instance);
        Context.TimeProvider.Returns(Time); Context.Parameters.Returns(_ => Parameters);
        Context.RequestFunctionAsync<UpdateVerticalSpreadTradePlanCommand, VerticalSpreadTradePlanId, FunctionResult<VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>>>(Arg.Any<UpdateVerticalSpreadTradePlanCommand>())
            .Returns(call => Plan(call.Arg<UpdateVerticalSpreadTradePlanCommand>()));
        Context.RequestAsync<StartVerticalSpreadExitPositionWorkflowCommand,ExitPositionWorkflowId>(Arg.Any<StartVerticalSpreadExitPositionWorkflowCommand>())
            .Returns(call=>RequestExit(call.Arg<StartVerticalSpreadExitPositionWorkflowCommand>()));
        Context.SendAsync<StartVerticalSpreadExitPositionWorkflowCommand, ExitPositionWorkflowId>(Arg.Any<StartVerticalSpreadExitPositionWorkflowCommand>(), Arg.Any<ExitPositionWorkflowId>())
            .Returns(call => Send(call.Arg<StartVerticalSpreadExitPositionWorkflowCommand>()));
        supervisor = Substitute.For<IActorSupervisor>(); var container = Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_ => new ActorThreadQueueV2(16)); supervisor.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        Pool = new(supervisor, NullLogger.Instance); Pool.Initialize(1); supervisor.ThreadPool.Returns(Pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call => Pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(), Arg.Any<CancellationToken>()).Returns(call => Pool.GetThreadAsync(call.Arg<ActorThreadId>(), call.Arg<CancellationToken>()));
        Queue = new(supervisor, id); var mailbox = Substitute.For<IActorMailbox>();
        mailbox.ThreadQueues.Returns(Queue.ThreadQueues); mailbox.Metrics.Returns((IActorMetricsStore)null!);
        supervisor.CreateMailbox(id).Returns(metrics ? Queue : mailbox);
        Actor = factory?.Invoke(Context) ?? new VerticalSpreadTradePositionRealtimeActor(Context);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [id] = Actor });
    }
    async ValueTask<ServiceResult<FunctionResult<VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>>>> Plan(UpdateVerticalSpreadTradePlanCommand command)
    {
        Requests.Add(command); Entered.TrySetResult(); if (Pause is not null) await Pause.Task;
        if (Failure == "throw") throw new IOException("Injected required plan transport failure.");
        if (Failure == "terminal") return new ServiceOk<FunctionResult<VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>>>(
            FunctionResult<VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>>.Fail(new() { CommandId = command.CommandId, EntityId = command.EntityId, ErrorData = "INJECTED_FAILURE", ErrorMessage = "Failed terminal" }));
        var completed = new VerticalSpreadTradePlanUpdatedEvent
        {
            Id = command.CommandId, CommandId = command.CommandId, EntityId = command.EntityId, SourceEventId = command.SourceEventId,
            ReceivedOn = command.Position.AsOfUtc, RequestFingerprint = command.Fingerprint(),
            Plan = new() { Position = command.Position, ValueDate = command.EntityId.ValueDate, PlanRevision = command.Position.PositionSequence,
                State = RequiresExit ? TradePlanState.ExitRequired : TradePlanState.Normal, RequiresExit = RequiresExit, ReasonCode = "test", CalculatedAtUtc = command.RequestedAtUtc }
        };
        if (Commit is not null) completed = await Commit(command, completed);
        else if (Receipts.TryGetValue(command.CommandId, out var original))
        {
            if (original.RequestFingerprint != completed.RequestFingerprint) throw new InvalidDataException("Same plan ID with different parameters.");
            completed = original;
        }
        else Receipts.Add(command.CommandId, completed);
        if (Failure == "lost-reply") throw new IOException("Injected loss after plan receipt commit.");
        return new ServiceOk<FunctionResult<VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>>>(
            FunctionResult<VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>>.Complete(completed));
    }
    async ValueTask<ServiceResult<GuidResult>> RequestExit(StartVerticalSpreadExitPositionWorkflowCommand command)
    {
        await Send(command);
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }
    async ValueTask Send(StartVerticalSpreadExitPositionWorkflowCommand command)
    {
        ExitAttempts.Add(command);
        if (Failure == "exit-send") throw new IOException("Injected exit command failure before acceptance.");
        if (Accept is not null) await Accept(command);
        AcceptedExits.Add(command);
        if (Failure == "exit-ambiguous") throw new IOException("Injected exit command failure after acceptance.");
    }
    public static VerticalSpreadPositionChangedEvent Input(int sequence)
    {
        var position = new StrategyPositionSnapshot { Id = StrategyPositionId.Create(new(1, 1, 1, 1), TradeStrategyKind.VerticalSpread),
            StrategyKind = TradeStrategyKind.VerticalSpread, IsOpen = true, PositionSequence = sequence, AsOfUtc = At };
        var id = new Guid(sequence, 0, 0, new byte[8]);
        return new() { Id = id, CommandId = id, EntityId = position.Id, PositionSnapshot = position, ReceivedOn = At,
            Subject = new(ActorType.Realtime, VerticalSpreadTradePositionRealtimeActor.ActorName, PositionChangedEvent.Verb, position.Id.Format()) };
    }
    public ValueTask Start() => Actor.StartAsync(supervisor);
    public async Task<bool> Deliver(VerticalSpreadPositionChangedEvent source)
    {
        var message = new Delivery(source);
        if (!await Queue.ThreadQueues.WriteAsync(message)) throw new IOException("Fixture admission failed.");
        await message.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5)); return await message.Settled.Task;
    }
    public void Reset() { Requests.Clear(); Receipts.Clear(); ExitAttempts.Clear(); AcceptedExits.Clear(); Context.ClearReceivedCalls(); }
    public async ValueTask DisposeAsync() { Pause?.TrySetResult(); await Actor.StopAsync(); await Pool.DisposeAsync(); await PositionIntents.DisposeAsync(); }
    public sealed class Clock : TimeProvider { public DateTimeOffset Now = new(At); public override DateTimeOffset GetUtcNow() => Now; }
    sealed class Delivery(VerticalSpreadPositionChangedEvent source) : IActorMessage, IActorDeliveryCompletion
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
