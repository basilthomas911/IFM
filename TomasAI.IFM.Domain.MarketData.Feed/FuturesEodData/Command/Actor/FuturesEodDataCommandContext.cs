using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Actor;

/// <summary>Defines the runtime services required by <see cref="FuturesEodDataCommandActor"/>.</summary>
public interface IFuturesEodDataCommandContext : ICommandActorContext<FuturesEodDataCommandActor>
{
    /// <summary>Gets the database factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the Market Data database.</summary>
    IMarketDataDbContext MarketDataDb { get; }
    /// <summary>Gets the blackboard service.</summary>
    IBlackboardService BlackboardService { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesEodDataCommandActor> Logger { get; }
    /// <summary>Gets the event-source database.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the durable replay queue.</summary>
    IDurableReplayQueue DurableReplayQueue { get; }
    /// <summary>Gets the state factory.</summary>
    IEventSourceActorStateFactory StateFactory { get; }
    /// <summary>Gets the actor service.</summary>
    IActorService ActorService { get; }
    /// <summary>Gets the event projector.</summary>
    IEventProjector<FuturesEodDataCommandActor> EventProjector { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesEodDataCommandActor"/>.</summary>
public sealed class FuturesEodDataCommandContext : CommandActorContext,
    ICommandActorContext<FuturesEodDataCommandActor>, IFuturesEodDataCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> _dbEventSource;
    readonly Lazy<IDurableReplayQueue> _durableReplayQueue;
    readonly Lazy<IEventSourceActorStateFactory> _stateFactory;
    readonly Lazy<IActorService> _actorService;
    readonly Lazy<IEventProjector<FuturesEodDataCommandActor>> _eventProjector;

    /// <summary>Initializes the typed command context.</summary>
    public FuturesEodDataCommandContext(IActorSupervisor supervisor, IDbContextFactory dbFactory,
        IBlackboardService blackboardService, ILogger<FuturesEodDataCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, FuturesEodDataCommandActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        BlackboardService = IsArgumentNull.Set(blackboardService);
        Logger = IsArgumentNull.Set(logger);
        _dbEventSource = ResolveOnce<IEventSourceActorDbContext>();
        _durableReplayQueue = ResolveOnce<IDurableReplayQueue>();
        _stateFactory = ResolveOnce<IEventSourceActorStateFactory>();
        _actorService = ResolveOnce<IActorService>();
        _eventProjector = ResolveOnce<IEventProjector<FuturesEodDataCommandActor>>();
    }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public IMarketDataDbContext MarketDataDb => DbFactory.MarketDataDb;
    /// <inheritdoc/>
    public IBlackboardService BlackboardService { get; }
    /// <inheritdoc/>
    public ILogger<FuturesEodDataCommandActor> Logger { get; }
    /// <inheritdoc/>
    public IEventSourceActorDbContext DbEventSource => _dbEventSource.Value;
    /// <inheritdoc/>
    public IDurableReplayQueue DurableReplayQueue => _durableReplayQueue.Value;
    /// <inheritdoc/>
    public IEventSourceActorStateFactory StateFactory => _stateFactory.Value;
    /// <inheritdoc/>
    public IActorService ActorService => _actorService.Value;
    /// <inheritdoc/>
    public IEventProjector<FuturesEodDataCommandActor> EventProjector => _eventProjector.Value;
    Lazy<T> ResolveOnce<T>() where T : class => new(() => IsArgumentNull.Set(Container.Resolve<T>())!);
}

