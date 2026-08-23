using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Actor;

/// <summary>Defines the runtime services required by <see cref="FuturesClosingPriceCommandActor"/>.</summary>
public interface IFuturesClosingPriceCommandContext : ICommandActorContext<FuturesClosingPriceCommandActor>
{
    /// <summary>Gets the database factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the Market Data database.</summary>
    IMarketDataDbContext MarketDataDb { get; }
    /// <summary>Gets the blackboard service.</summary>
    IBlackboardService BlackboardService { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesClosingPriceCommandActor> Logger { get; }
    /// <summary>Gets the event-source database.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the durable replay queue.</summary>
    IDurableReplayQueue DurableReplayQueue { get; }
    /// <summary>Gets the state factory.</summary>
    IEventSourceActorStateFactory StateFactory { get; }
    /// <summary>Gets the actor service.</summary>
    IActorService ActorService { get; }
    /// <summary>Gets the event projector.</summary>
    IEventProjector<FuturesClosingPriceCommandActor> EventProjector { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesClosingPriceCommandActor"/>.</summary>
public sealed class FuturesClosingPriceCommandContext : CommandActorContext,
    ICommandActorContext<FuturesClosingPriceCommandActor>, IFuturesClosingPriceCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> _dbEventSource;
    readonly Lazy<IDurableReplayQueue> _durableReplayQueue;
    readonly Lazy<IEventSourceActorStateFactory> _stateFactory;
    readonly Lazy<IActorService> _actorService;
    readonly Lazy<IEventProjector<FuturesClosingPriceCommandActor>> _eventProjector;

    /// <summary>Initializes the typed command context.</summary>
    public FuturesClosingPriceCommandContext(IActorSupervisor supervisor, IDbContextFactory dbFactory,
        IBlackboardService blackboardService, ILogger<FuturesClosingPriceCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, FuturesClosingPriceCommandActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        BlackboardService = IsArgumentNull.Set(blackboardService);
        Logger = IsArgumentNull.Set(logger);
        _dbEventSource = ResolveOnce<IEventSourceActorDbContext>();
        _durableReplayQueue = ResolveOnce<IDurableReplayQueue>();
        _stateFactory = ResolveOnce<IEventSourceActorStateFactory>();
        _actorService = ResolveOnce<IActorService>();
        _eventProjector = ResolveOnce<IEventProjector<FuturesClosingPriceCommandActor>>();
    }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public IMarketDataDbContext MarketDataDb => DbFactory.MarketDataDb;
    /// <inheritdoc/>
    public IBlackboardService BlackboardService { get; }
    /// <inheritdoc/>
    public ILogger<FuturesClosingPriceCommandActor> Logger { get; }
    /// <inheritdoc/>
    public IEventSourceActorDbContext DbEventSource => _dbEventSource.Value;
    /// <inheritdoc/>
    public IDurableReplayQueue DurableReplayQueue => _durableReplayQueue.Value;
    /// <inheritdoc/>
    public IEventSourceActorStateFactory StateFactory => _stateFactory.Value;
    /// <inheritdoc/>
    public IActorService ActorService => _actorService.Value;
    /// <inheritdoc/>
    public IEventProjector<FuturesClosingPriceCommandActor> EventProjector => _eventProjector.Value;
    Lazy<T> ResolveOnce<T>() where T : class => new(() => IsArgumentNull.Set(Container.Resolve<T>())!);
}

