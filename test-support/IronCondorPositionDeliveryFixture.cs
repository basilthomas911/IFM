using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime;
using IronCondorTradePlanId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.TestSupport;

/// <summary>Real position actor/scheduler. Function receipts, pricing, cache and realtime publication are substituted.</summary>
public sealed class IronCondorPositionDeliveryFixture : IAsyncDisposable
{
    public static readonly DateTime At = new(2026, 8, 25, 14, 30, 0, DateTimeKind.Utc);
    public static readonly Guid MonitoringGeneration = new("7ae21f70-793b-4889-8d25-386bbbf03009");
    public readonly IIronCondorTradePositionRealtimeContext Context = Substitute.For<IIronCondorTradePositionRealtimeContext>();
    public readonly ActorThreadPoolV2 Pool;
    public readonly ActorMailbox Queue;
    public readonly IActor Actor;
    readonly IActorSupervisor supervisor;
    public readonly Clock Time = new();
    public readonly IronCondorPositionRequests Requests;
    public IIronCondorTradePositionRealtimeContext? PublicationServices;
    public IronCondorTradePositionRealtimeContext? PublicationContext;
    public TradeRealtimeCacheRegistry? PublicationCache;
    public readonly List<TradeRealtimeSnapshotChangedEvent> Notifications = [];
    public Func<TradeRealtimeSnapshotChangedEvent, Task>? Publish;
    public Action? BeforePublication, AfterPublication;
    public bool FailBeforePublication, FailAfterPublication, PublicationPlanOnly;
    public bool ForwardFails;
    public Action? BeforeForward, BeforePlan;
    public TradePlanParameters Parameters = new();
    public string Failure = "none";
    public bool Closing, Current = true, InputsAvailable = true;
    public IronCondorTradePlanInputs Inputs = new() { PutForwardPrice = 5, CallForwardPrice = 6, TradeDate = new(2026, 8, 21), MaturityDate = new(2026, 9, 25) };
    public readonly List<CalculateIronCondorForwardPriceCommand> ForwardRequests = [];
    public readonly List<UpdateIronCondorTradePlanCommand> PlanRequests = [];
    public readonly Dictionary<Guid, IronCondorTradePlanUpdatedEvent> Receipts = [];
    public readonly List<long> PublishedPlans = [];
    public Func<UpdateIronCondorTradePlanCommand, IronCondorTradePlanUpdatedEvent, Task<IronCondorTradePlanUpdatedEvent>>? Commit;
    public TaskCompletionSource? Pause;
    public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public IronCondorPositionDeliveryFixture(bool metrics, Func<IIronCondorTradePositionRealtimeContext, IActor>? factory = null, string? recoveryDirectory = null, bool realPublication = false, Func<IActorSupervisor, TradeRealtimeCacheRegistry, IronCondorPositionRequests, TimeProvider, IIronCondorTradePositionRealtimeContext>? publicationFactory = null)
    {
        Requests = new(recoveryDirectory); Context.Requests.Returns(Requests);
        var id = new ActorMailboxId(ActorType.Realtime, IronCondorTradePositionRealtimeActor.ActorName);
        Context.ActorId.Returns(id); Context.Logger.Returns(NullLogger<IronCondorTradePositionRealtimeActor>.Instance); Context.TimeProvider.Returns(Time);
        Context.Parameters.Returns(_ => Parameters);
        Context.IsClosingAsync(Arg.Any<StrategyPositionId>()).Returns(_ => ValueTask.FromResult(Closing));
        Context.IsCurrentMonitoringPosition(Arg.Any<StrategyPositionSnapshot>(), Arg.Any<Guid>()).Returns(_ => Current);
        Context.CaptureMonitoringInputs(Arg.Any<StrategyPositionSnapshot>(), Arg.Any<DateOnly>(), Arg.Any<DateTime>()).Returns(_ => InputsAvailable ? Inputs : null);
        Context.RequestFunctionAsync<CalculateIronCondorForwardPriceCommand, IronCondorTradePlanId, FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>(Arg.Any<CalculateIronCondorForwardPriceCommand>())
            .Returns(call => Forward(call.Arg<CalculateIronCondorForwardPriceCommand>()));
        Context.RequestFunctionAsync<UpdateIronCondorTradePlanCommand, IronCondorTradePlanId, FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>(Arg.Any<UpdateIronCondorTradePlanCommand>())
            .Returns(call => Plan(call.Arg<UpdateIronCondorTradePlanCommand>()));
        Context.PublishRealtimeAsync(Arg.Any<StrategyPositionSnapshot>(), Arg.Any<StrategyTradePlanSnapshot?>()).Returns(call =>
        {
            if (realPublication)
                return PublicationServices!.PublishRealtimeAsync(call.ArgAt<StrategyPositionSnapshot>(0), call.ArgAt<StrategyTradePlanSnapshot?>(1));
            if (call.ArgAt<StrategyTradePlanSnapshot?>(1) is not { } plan) return ValueTask.CompletedTask;
            if (Failure == "publication") return ValueTask.FromException(new IOException("Injected final realtime plan publication failure."));
            PublishedPlans.Add(plan.PlanRevision); return ValueTask.CompletedTask;
        });
        supervisor = Substitute.For<IActorSupervisor>(); var container = Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_ => new ActorThreadQueueV2(16)); supervisor.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        if (realPublication)
        {
            PublicationCache = new(Time);
            PublicationCache.Register(new EstablishedTradeDefinition { Id = Position(1).Id.Trade, StrategyKind = TradeStrategyKind.IronCondor }, Position(1).ValueDate);
            var producer = Substitute.For<IActorProducer>(); supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(producer);
            producer.SendAsync<TradeRealtimeSnapshotChangedEvent, TradeEntityId>(Arg.Any<ActorSubject>(), Arg.Any<TradeRealtimeSnapshotChangedEvent>(), Arg.Any<CancellationToken>())
                .Returns(call => new ValueTask(SendNotification(call.Arg<TradeRealtimeSnapshotChangedEvent>())));
            PublicationServices = publicationFactory?.Invoke(supervisor, PublicationCache, Requests, Time) ?? new IronCondorTradePositionRealtimeContext(supervisor, NullLogger<IronCondorTradePositionRealtimeActor>.Instance, realtimeCache: PublicationCache, requests: Requests, timeProvider: Time);
            PublicationContext = PublicationServices as IronCondorTradePositionRealtimeContext;
            Context.RecoverPublicationsAsync().Returns(_ => PublicationServices.RecoverPublicationsAsync());
        }
        Pool = new(supervisor, NullLogger.Instance); Pool.Initialize(1); supervisor.ThreadPool.Returns(Pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call => Pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(), Arg.Any<CancellationToken>()).Returns(call => Pool.GetThreadAsync(call.Arg<ActorThreadId>(), call.Arg<CancellationToken>()));
        Queue = new(supervisor, id); var mailbox = Substitute.For<IActorMailbox>();
        mailbox.ThreadQueues.Returns(Queue.ThreadQueues); mailbox.Metrics.Returns((IActorMetricsStore)null!);
        supervisor.CreateMailbox(id).Returns(metrics ? Queue : mailbox);
        Actor = factory?.Invoke(Context) ?? new IronCondorTradePositionRealtimeActor(Context);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [id] = Actor });
    }
    async Task SendNotification(TradeRealtimeSnapshotChangedEvent notification)
    {
        var eligible = !PublicationPlanOnly || notification.Snapshot.Plan is not null;
        if (eligible) BeforePublication?.Invoke();
        if (eligible && FailBeforePublication) throw new IOException("Injected failure before send.");
        if (Publish is not null) await Publish(notification);
        Notifications.Add(notification);
        if (eligible) AfterPublication?.Invoke();
        if (eligible && FailAfterPublication) throw new IOException("Injected ambiguous send after acceptance.");
    }
    ValueTask<ServiceResult<FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>> Forward(CalculateIronCondorForwardPriceCommand command)
    {
        ForwardRequests.Add(command); BeforeForward?.Invoke();
        if (ForwardFails) throw new IOException("Injected forward response loss.");
        var completed = new IronCondorForwardPriceCalculatedEvent { Id = command.CommandId, CommandId = command.CommandId, EntityId = command.EntityId, SourceEventId = command.SourceEventId, ReceivedOn = command.Position.AsOfUtc, RequestFingerprint = command.Fingerprint() };
        return ValueTask.FromResult<ServiceResult<FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>>(
            new ServiceOk<FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>(FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>.Complete(completed)));
    }
    async ValueTask<ServiceResult<FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>> Plan(UpdateIronCondorTradePlanCommand command)
    {
        PlanRequests.Add(command); BeforePlan?.Invoke(); Entered.TrySetResult(); if (Pause is not null) await Pause.Task;
        if (Failure == "throw") throw new IOException("Injected required plan request failure before commit.");
        if (Failure is "terminal" or "superseded")
            return new ServiceOk<FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>(FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>.Fail(new()
            { EntityId = command.EntityId, CommandId = command.CommandId, ErrorData = Failure == "superseded" ? "IronCondorTradePlan.OBSERVATION.SUPERSEDED" : "INJECTED_PLAN_FAILURE", ErrorMessage = "Injected failed terminal" }));
        var completed = new IronCondorTradePlanUpdatedEvent
        {
            Id = command.CommandId, CommandId = command.CommandId, EntityId = command.EntityId, SourceEventId = command.SourceEventId, RequestFingerprint = command.Fingerprint(),
            Plan = new() { Position = command.Position, ValueDate = command.EntityId.ValueDate, PlanRevision = command.Position.PositionSequence, CalculatedAtUtc = command.RequestedAtUtc }
        };
        if (Commit is not null) completed = await Commit(command, completed);
        else if (Receipts.TryGetValue(command.CommandId, out var original))
        {
            if (original.RequestFingerprint != completed.RequestFingerprint) throw new InvalidDataException("Same plan command ID has changed captured inputs.");
            completed = original;
        }
        else Receipts.Add(command.CommandId, completed);
        if (Failure == "lost-reply") throw new IOException("Injected reply loss after plan receipt commit.");
        return new ServiceOk<FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>(FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>.Complete(completed));
    }
    public static StrategyPositionSnapshot Position(int sequence) => new()
    { Id = StrategyPositionId.Create(new(1, 1, 1, 1), TradeStrategyKind.IronCondor), StrategyKind = TradeStrategyKind.IronCondor, IsOpen = true, PositionSequence = sequence, AsOfUtc = At, ValueDate = new(2026, 8, 25) };
    public IEvent Input(string kind, int sequence)
    {
        var position = Position(sequence); var id = new Guid(sequence, 0, 0, new byte[8]);
        if (kind == "Position") return new IronCondorPositionChangedEvent
        { Id = id, CommandId = id, EntityId = position.Id, PositionSnapshot = position, ReceivedOn = At, Subject = new(ActorType.Realtime, IronCondorTradePositionRealtimeActor.ActorName, PositionChangedEvent.Verb, position.Id.Format()) };
        return new IronCondorMonitoringInputsChangedEvent
        { Id = id, CommandId = id, EntityId = position.Id, PositionSnapshot = position, ReceivedOn = At, ValueDate = new(2026, 8, 25), IronCondorTradePlanInputs = Inputs, MonitoringGenerationId = MonitoringGeneration,
            Subject = new(ActorType.Realtime, IronCondorTradePositionRealtimeActor.ActorName, IronCondorMonitoringInputsChangedEvent.Verb, position.Id.Format()) };
    }
    public ValueTask Start() => Actor.StartAsync(supervisor);
    public async Task<bool> Deliver(IEvent source)
    {
        var message = new Delivery(source);
        if (!await Queue.ThreadQueues.WriteAsync(message)) throw new IOException("Position fixture admission failed.");
        await message.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5)); return await message.Settled.Task;
    }
    public void Reset() { ForwardRequests.Clear(); PlanRequests.Clear(); PublishedPlans.Clear(); Receipts.Clear(); Notifications.Clear(); Context.ClearReceivedCalls(); }
    public async ValueTask DisposeAsync() { Pause?.TrySetResult(); await Actor.StopAsync(); await Pool.DisposeAsync(); Requests.Dispose(); }
    public sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(At);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    sealed class Delivery(IEvent source) : IActorMessage, IActorDeliveryCompletion
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
