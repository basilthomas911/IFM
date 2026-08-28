using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;

/// <summary>Defines readonly services owned by the Intrinsic Time Strategy Workflow Command actor.</summary>
public interface IIntrinsicTimeStrategyWorkflowCommandContext
    : ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor>
{
    /// <summary>Gets the EventSourceDb context.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the application database-context factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the conventional workflow projector.</summary>
    IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor> EventProjector { get; }
    /// <summary>Gets the authoritative event-source repository.</summary>
    IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState> StateRepository { get; }
    /// <summary>Gets the non-durable queue dependency required by the projector base.</summary>
    IDurableReplayQueue DurableReplayQueue { get; }
    /// <summary>Gets the blackboard service required by the projector base.</summary>
    IBlackboardService BlackboardService { get; }
    /// <summary>Gets the event-source state factory.</summary>
    IEventSourceActorStateFactory StateFactory { get; }
    /// <summary>Gets the actor service used by the state repository.</summary>
    IActorService ActorService { get; }
    /// <summary>Gets the workflow clock.</summary>
    TimeProvider TimeProvider { get; }
    /// <summary>Gets the validated fixed workflow execution duration.</summary>
    RegimeDiscoveryExecutionOptions ExecutionOptions { get; }
    /// <summary>Gets the command actor logger.</summary>
    ILogger<IntrinsicTimeStrategyWorkflowCommandActor> Logger { get; }
}

/// <summary>Provides the closed-generic Command context for the workflow aggregate.</summary>
public sealed class IntrinsicTimeStrategyWorkflowCommandContext
    : CommandActorContext,
      ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor>,
      IIntrinsicTimeStrategyWorkflowCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> _dbEventSource;
    readonly Lazy<IDurableReplayQueue> _durableReplayQueue;
    readonly Lazy<IEventSourceActorStateFactory> _stateFactory;
    readonly Lazy<IActorService> _actorService;
    readonly Lazy<IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor>> _eventProjector;
    readonly Lazy<IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>> _stateRepository;

    /// <summary>Initializes the workflow Command context and its readonly application dependencies.</summary>
    public IntrinsicTimeStrategyWorkflowCommandContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        IBlackboardService blackboardService,
        RegimeDiscoveryExecutionOptions executionOptions,
        ILogger<IntrinsicTimeStrategyWorkflowCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, IntrinsicTimeStrategyWorkflowCommandActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        BlackboardService = IsArgumentNull.Set(blackboardService);
        TimeProvider = TimeProvider.System;
        ExecutionOptions = IsArgumentNull.Set(executionOptions);
        Logger = IsArgumentNull.Set(logger);
        _dbEventSource = ResolveOnce<IEventSourceActorDbContext>();
        _durableReplayQueue = ResolveOnce<IDurableReplayQueue>();
        _stateFactory = ResolveOnce<IEventSourceActorStateFactory>();
        _actorService = ResolveOnce<IActorService>();
        _eventProjector = ResolveOnce<IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor>>();
        _stateRepository = ResolveOnce<IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>>();
    }

    /// <inheritdoc />
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc />
    public IBlackboardService BlackboardService { get; }
    /// <inheritdoc />
    public TimeProvider TimeProvider { get; }
    /// <inheritdoc />
    public RegimeDiscoveryExecutionOptions ExecutionOptions { get; }
    /// <inheritdoc />
    public ILogger<IntrinsicTimeStrategyWorkflowCommandActor> Logger { get; }
    /// <inheritdoc />
    public IEventSourceActorDbContext DbEventSource => _dbEventSource.Value;
    /// <inheritdoc />
    public IDurableReplayQueue DurableReplayQueue => _durableReplayQueue.Value;
    /// <inheritdoc />
    public IEventSourceActorStateFactory StateFactory => _stateFactory.Value;
    /// <inheritdoc />
    public IActorService ActorService => _actorService.Value;
    /// <inheritdoc />
    public IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor> EventProjector => _eventProjector.Value;
    /// <inheritdoc />
    public IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState> StateRepository => _stateRepository.Value;

    Lazy<TService> ResolveOnce<TService>() where TService : class
        => new(() => IsArgumentNull.Set(Container.Resolve<TService>())!);
}
