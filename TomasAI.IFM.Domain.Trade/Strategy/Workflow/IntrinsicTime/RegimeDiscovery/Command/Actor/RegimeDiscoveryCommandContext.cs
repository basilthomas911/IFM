using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Actor;

/// <summary>Defines the readonly dependencies owned by the Regime Discovery Command actor.</summary>
public interface IRegimeDiscoveryCommandContext : ICommandActorContext<RegimeDiscoveryCommandActor>
{
    /// <summary>Gets the PostgreSQL event-source context.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the application database-context factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the conventional Regime Discovery projector.</summary>
    IEventProjector<RegimeDiscoveryCommandActor> EventProjector { get; }
    /// <summary>Gets the authoritative event-source repository.</summary>
    IEventSourceActorStateRepository<RegimeDiscoveryCommandState> StateRepository { get; }
    /// <summary>Gets the projector queue dependency.</summary>
    IDurableReplayQueue DurableReplayQueue { get; }
    /// <summary>Gets the projector blackboard dependency.</summary>
    IBlackboardService BlackboardService { get; }
    /// <summary>Gets the event-source state factory.</summary>
    IEventSourceActorStateFactory StateFactory { get; }
    /// <summary>Gets the actor infrastructure service.</summary>
    IActorService ActorService { get; }
    /// <summary>Gets the atomic market-signal snapshot provider.</summary>
    IRegimeDiscoveryMarketSignalSnapshotProvider SnapshotProvider { get; }
    /// <summary>Gets the deterministic calculation coordinator.</summary>
    IRegimeDiscoveryCalculationModel CalculationModel { get; }
    /// <summary>Gets the specialist scheduling mode.</summary>
    RegimeDiscoveryExecutionMode ExecutionMode { get; }
    /// <summary>Gets the calculation clock.</summary>
    TimeProvider TimeProvider { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<RegimeDiscoveryCommandActor> Logger { get; }
}

/// <summary>Provides the closed-generic Command context for Regime Discovery.</summary>
public sealed class RegimeDiscoveryCommandContext
    : CommandActorContext,
      ICommandActorContext<RegimeDiscoveryCommandActor>,
      IRegimeDiscoveryCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> _dbEventSource;
    readonly Lazy<IDurableReplayQueue> _durableReplayQueue;
    readonly Lazy<IEventSourceActorStateFactory> _stateFactory;
    readonly Lazy<IActorService> _actorService;
    readonly Lazy<IEventProjector<RegimeDiscoveryCommandActor>> _eventProjector;
    readonly Lazy<IEventSourceActorStateRepository<RegimeDiscoveryCommandState>> _stateRepository;
    readonly Lazy<IRegimeDiscoveryMarketSignalSnapshotProvider> _snapshotProvider;

    /// <summary>Initializes the Regime Discovery Command context.</summary>
    public RegimeDiscoveryCommandContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        IBlackboardService blackboardService,
        ILogger<RegimeDiscoveryCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, RegimeDiscoveryCommandActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        BlackboardService = IsArgumentNull.Set(blackboardService);
        Logger = IsArgumentNull.Set(logger);
        TimeProvider = TimeProvider.System;
        CalculationModel = new RegimeDiscoveryCalculationModel();
        ExecutionMode = RegimeDiscoveryExecutionMode.Sequential;
        _dbEventSource = ResolveOnce<IEventSourceActorDbContext>();
        _durableReplayQueue = ResolveOnce<IDurableReplayQueue>();
        _stateFactory = ResolveOnce<IEventSourceActorStateFactory>();
        _actorService = ResolveOnce<IActorService>();
        _eventProjector = ResolveOnce<IEventProjector<RegimeDiscoveryCommandActor>>();
        _stateRepository = ResolveOnce<IEventSourceActorStateRepository<RegimeDiscoveryCommandState>>();
        _snapshotProvider = ResolveOnce<IRegimeDiscoveryMarketSignalSnapshotProvider>();
    }

    /// <inheritdoc />
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc />
    public IBlackboardService BlackboardService { get; }
    /// <inheritdoc />
    public ILogger<RegimeDiscoveryCommandActor> Logger { get; }
    /// <inheritdoc />
    public TimeProvider TimeProvider { get; }
    /// <inheritdoc />
    public IRegimeDiscoveryCalculationModel CalculationModel { get; }
    /// <inheritdoc />
    public RegimeDiscoveryExecutionMode ExecutionMode { get; }
    /// <inheritdoc />
    public IEventSourceActorDbContext DbEventSource => _dbEventSource.Value;
    /// <inheritdoc />
    public IDurableReplayQueue DurableReplayQueue => _durableReplayQueue.Value;
    /// <inheritdoc />
    public IEventSourceActorStateFactory StateFactory => _stateFactory.Value;
    /// <inheritdoc />
    public IActorService ActorService => _actorService.Value;
    /// <inheritdoc />
    public IEventProjector<RegimeDiscoveryCommandActor> EventProjector => _eventProjector.Value;
    /// <inheritdoc />
    public IEventSourceActorStateRepository<RegimeDiscoveryCommandState> StateRepository => _stateRepository.Value;
    /// <inheritdoc />
    public IRegimeDiscoveryMarketSignalSnapshotProvider SnapshotProvider => _snapshotProvider.Value;

    Lazy<TService> ResolveOnce<TService>() where TService : class
        => new(() => IsArgumentNull.Set(Container.Resolve<TService>())!);
}
