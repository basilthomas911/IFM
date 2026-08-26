using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Actor;

/// <summary>Defines the readonly services required by the Market Outlook command actor.</summary>
public interface IMarketOutlookSnapshotCommandContext
    : ICommandActorContext<MarketOutlookSnapshotCommandActor>
{
    /// <summary>Gets the event-source database.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the application database-context factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the authoritative state repository.</summary>
    IEventSourceActorStateRepository<MarketOutlookSnapshotCommandState> StateRepository { get; }
    /// <summary>Gets the command-owned event projector.</summary>
    IEventProjector<MarketOutlookSnapshotCommandActor> EventProjector { get; }
    /// <summary>Gets the durable projection queue.</summary>
    IDurableReplayQueue DurableReplayQueue { get; }
    /// <summary>Gets the projector blackboard.</summary>
    IBlackboardService BlackboardService { get; }
    /// <summary>Gets the event-source state factory.</summary>
    IEventSourceActorStateFactory StateFactory { get; }
    /// <summary>Gets the actor service.</summary>
    IActorService ActorService { get; }
    /// <summary>Gets the system clock.</summary>
    TimeProvider TimeProvider { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<MarketOutlookSnapshotCommandActor> Logger { get; }
}

/// <summary>Provides the closed-generic runtime context for the Market Outlook command actor.</summary>
public sealed class MarketOutlookSnapshotCommandContext
    : CommandActorContext,
      ICommandActorContext<MarketOutlookSnapshotCommandActor>,
      IMarketOutlookSnapshotCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> _dbEventSource;
    readonly Lazy<IEventSourceActorStateRepository<MarketOutlookSnapshotCommandState>> _stateRepository;
    readonly Lazy<IEventProjector<MarketOutlookSnapshotCommandActor>> _eventProjector;
    readonly Lazy<IDurableReplayQueue> _durableReplayQueue;
    readonly Lazy<IEventSourceActorStateFactory> _stateFactory;
    readonly Lazy<IActorService> _actorService;

    /// <summary>Initializes the immutable Market Outlook command context.</summary>
    public MarketOutlookSnapshotCommandContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        IBlackboardService blackboardService,
        ILogger<MarketOutlookSnapshotCommandActor> logger)
        : base(supervisor, new ActorMailboxId(
            ActorType.Command,
            MarketOutlookSnapshotCommandActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        BlackboardService = IsArgumentNull.Set(blackboardService);
        Logger = IsArgumentNull.Set(logger);
        TimeProvider = TimeProvider.System;
        _dbEventSource = ResolveOnce<IEventSourceActorDbContext>();
        _stateRepository = ResolveOnce<IEventSourceActorStateRepository<MarketOutlookSnapshotCommandState>>();
        _eventProjector = ResolveOnce<IEventProjector<MarketOutlookSnapshotCommandActor>>();
        _durableReplayQueue = ResolveOnce<IDurableReplayQueue>();
        _stateFactory = ResolveOnce<IEventSourceActorStateFactory>();
        _actorService = ResolveOnce<IActorService>();
    }

    /// <inheritdoc />
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc />
    public IBlackboardService BlackboardService { get; }
    /// <inheritdoc />
    public TimeProvider TimeProvider { get; }
    /// <inheritdoc />
    public ILogger<MarketOutlookSnapshotCommandActor> Logger { get; }
    /// <inheritdoc />
    public IEventSourceActorDbContext DbEventSource => _dbEventSource.Value;
    /// <inheritdoc />
    public IEventSourceActorStateRepository<MarketOutlookSnapshotCommandState> StateRepository =>
        _stateRepository.Value;
    /// <inheritdoc />
    public IEventProjector<MarketOutlookSnapshotCommandActor> EventProjector => _eventProjector.Value;
    /// <inheritdoc />
    public IDurableReplayQueue DurableReplayQueue => _durableReplayQueue.Value;
    /// <inheritdoc />
    public IEventSourceActorStateFactory StateFactory => _stateFactory.Value;
    /// <inheritdoc />
    public IActorService ActorService => _actorService.Value;

    Lazy<TService> ResolveOnce<TService>() where TService : class
        => new(() => IsArgumentNull.Set(Container.Resolve<TService>())!);
}
