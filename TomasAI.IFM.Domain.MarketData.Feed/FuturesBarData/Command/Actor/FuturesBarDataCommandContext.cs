using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesBarDataCommandActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesBarDataCommandContext : CommandActorContext,
IFuturesBarDataCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> _dbEventSource;
    readonly Lazy<IDurableReplayQueue> _durableReplayQueue;
    readonly Lazy<IEventSourceActorStateFactory> _stateFactory;
    readonly Lazy<IActorService> _actorService;
    readonly Lazy<IEventProjector<FuturesBarDataCommandActor>> _eventProjector;

    /// <summary>Initializes the typed command context.</summary>
    public FuturesBarDataCommandContext(IActorSupervisor supervisor, IDbContextFactory dbFactory,
        IBlackboardService blackboardService, ILogger<FuturesBarDataCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, FuturesBarDataCommandActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        BlackboardService = IsArgumentNull.Set(blackboardService);
        Logger = IsArgumentNull.Set(logger);
        _dbEventSource = ResolveOnce<IEventSourceActorDbContext>();
        _durableReplayQueue = ResolveOnce<IDurableReplayQueue>();
        _stateFactory = ResolveOnce<IEventSourceActorStateFactory>();
        _actorService = ResolveOnce<IActorService>();
        _eventProjector = ResolveOnce<IEventProjector<FuturesBarDataCommandActor>>();
    }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public IMarketDataDbContext MarketDataDb => DbFactory.MarketDataDb;
    /// <inheritdoc/>
    public IBlackboardService BlackboardService { get; }
    /// <inheritdoc/>
    public ILogger<FuturesBarDataCommandActor> Logger { get; }
    /// <inheritdoc/>
    public IEventSourceActorDbContext DbEventSource => _dbEventSource.Value;
    /// <inheritdoc/>
    public IDurableReplayQueue DurableReplayQueue => _durableReplayQueue.Value;
    /// <inheritdoc/>
    public IEventSourceActorStateFactory StateFactory => _stateFactory.Value;
    /// <inheritdoc/>
    public IActorService ActorService => _actorService.Value;
    /// <inheritdoc/>
    public IEventProjector<FuturesBarDataCommandActor> EventProjector => _eventProjector.Value;
    Lazy<T> ResolveOnce<T>() where T : class => new(() => IsArgumentNull.Set(Container.Resolve<T>())!);
}

