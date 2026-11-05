using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
/// <summary>Provides the closed-generic Command context for the workflow aggregate.</summary>
public sealed class IntrinsicTimeStrategyWorkflowCommandContext : CommandActorContext, IIntrinsicTimeStrategyWorkflowCommandContext
{
    public Domain.Portfolio.Shared.ServiceApi.IPortfolioQueryApi PortfolioQueries => Container.Resolve<Domain.Portfolio.Shared.ServiceApi.IPortfolioQueryApi>();
    public Domain.Portfolio.Shared.Financial.IPortfolioFinancialApi FinancialApi => Container.Resolve<Domain.Portfolio.Shared.Financial.IPortfolioFinancialApi>();

    readonly Lazy<IEventSourceActorDbContext> _dbEventSource;
    readonly Lazy<IDurableReplayQueue> _durableReplayQueue;
    readonly Lazy<IEventSourceActorStateFactory> _stateFactory;
    readonly Lazy<IActorService> _actorService;
    readonly Lazy<IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor>> _eventProjector;
    readonly Lazy<IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>> _stateRepository;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    /// <summary>Initializes the workflow Command context and its readonly application dependencies.</summary>
    public IntrinsicTimeStrategyWorkflowCommandContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, IBlackboardService blackboardService, RegimeDiscoveryExecutionOptions executionOptions, IntrinsicTimeStrategyWorkflowOptions workflowOptions, ILogger<IntrinsicTimeStrategyWorkflowCommandActor> logger) : base(supervisor, new ActorMailboxId(ActorType.Command, IntrinsicTimeStrategyWorkflowCommandActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        BlackboardService = IsArgumentNull.Set(blackboardService);
        TimeProvider = TimeProvider.System;
        ExecutionOptions = IsArgumentNull.Set(executionOptions);
        WorkflowStartsEnabled = IsArgumentNull.Set(workflowOptions).Enabled;
        Logger = IsArgumentNull.Set(logger);
        _dbEventSource = ResolveOnce<IEventSourceActorDbContext>();
        _durableReplayQueue = ResolveOnce<IDurableReplayQueue>();
        _stateFactory = ResolveOnce<IEventSourceActorStateFactory>();
        _actorService = ResolveOnce<IActorService>();
        _eventProjector = ResolveOnce<IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor>>();
        _stateRepository = ResolveOnce<IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>>();
    }

    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public IBlackboardService BlackboardService { get; }
    /// <inheritdoc/>
    public TimeProvider TimeProvider { get; }
    /// <inheritdoc/>
    public RegimeDiscoveryExecutionOptions ExecutionOptions { get; }
    /// <summary>Gets whether this host admits new signal-triggered workflows.</summary>
    public bool WorkflowStartsEnabled { get; }
    /// <inheritdoc/>
    public ILogger<IntrinsicTimeStrategyWorkflowCommandActor> Logger { get; }
    /// <inheritdoc/>
    public IEventSourceActorDbContext DbEventSource => _dbEventSource.Value;
    /// <inheritdoc/>
    public IDurableReplayQueue DurableReplayQueue => _durableReplayQueue.Value;
    /// <inheritdoc/>
    public IEventSourceActorStateFactory StateFactory => _stateFactory.Value;
    /// <inheritdoc/>
    public IActorService ActorService => _actorService.Value;
    /// <inheritdoc/>
    public IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor> EventProjector => _eventProjector.Value;
    /// <inheritdoc/>
    public IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState> StateRepository => _stateRepository.Value;

    Lazy<TService> ResolveOnce<TService>()
        where TService : class => new(() => IsArgumentNull.Set(Container.Resolve<TService>())!);
}
