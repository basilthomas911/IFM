using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Actor;

/// <summary>Defines the runtime services required by <see cref="FuturesOptionTickDataCommandActor"/>.</summary>
public interface IFuturesOptionTickDataCommandContext : ICommandActorContext<FuturesOptionTickDataCommandActor>
{
    /// <summary>Gets the database factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the Market Data database.</summary>
    IMarketDataDbContext MarketDataDb { get; }
    /// <summary>Gets the blackboard service.</summary>
    IBlackboardService BlackboardService { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesOptionTickDataCommandActor> Logger { get; }
    /// <summary>Gets the event-source database.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the durable replay queue.</summary>
    IDurableReplayQueue DurableReplayQueue { get; }
    /// <summary>Gets the state factory.</summary>
    IEventSourceActorStateFactory StateFactory { get; }
    /// <summary>Gets the actor service.</summary>
    IActorService ActorService { get; }
    /// <summary>Gets the event projector.</summary>
    IEventProjector<FuturesOptionTickDataCommandActor> EventProjector { get; }
    /// <summary>Gets the sequence generator.</summary>
    ISequenceIdGenerator SequenceIdGenerator { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesOptionTickDataCommandActor"/>.</summary>
public sealed class FuturesOptionTickDataCommandContext : CommandActorContext,
    ICommandActorContext<FuturesOptionTickDataCommandActor>, IFuturesOptionTickDataCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> _dbEventSource;
    readonly Lazy<IDurableReplayQueue> _durableReplayQueue;
    readonly Lazy<IEventSourceActorStateFactory> _stateFactory;
    readonly Lazy<IActorService> _actorService;
    readonly Lazy<IEventProjector<FuturesOptionTickDataCommandActor>> _eventProjector;
    readonly Lazy<ISequenceIdGenerator> _sequenceIdGenerator;

    /// <summary>Initializes the typed command context.</summary>
    public FuturesOptionTickDataCommandContext(IActorSupervisor supervisor, IDbContextFactory dbFactory,
        IBlackboardService blackboardService, ILogger<FuturesOptionTickDataCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, FuturesOptionTickDataCommandActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        BlackboardService = IsArgumentNull.Set(blackboardService);
        Logger = IsArgumentNull.Set(logger);
        _dbEventSource = ResolveOnce<IEventSourceActorDbContext>();
        _durableReplayQueue = ResolveOnce<IDurableReplayQueue>();
        _stateFactory = ResolveOnce<IEventSourceActorStateFactory>();
        _actorService = ResolveOnce<IActorService>();
        _eventProjector = ResolveOnce<IEventProjector<FuturesOptionTickDataCommandActor>>();
        _sequenceIdGenerator = ResolveOnce<ISequenceIdGenerator>();
    }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public IMarketDataDbContext MarketDataDb => DbFactory.MarketDataDb;
    /// <inheritdoc/>
    public IBlackboardService BlackboardService { get; }
    /// <inheritdoc/>
    public ILogger<FuturesOptionTickDataCommandActor> Logger { get; }
    /// <inheritdoc/>
    public IEventSourceActorDbContext DbEventSource => _dbEventSource.Value;
    /// <inheritdoc/>
    public IDurableReplayQueue DurableReplayQueue => _durableReplayQueue.Value;
    /// <inheritdoc/>
    public IEventSourceActorStateFactory StateFactory => _stateFactory.Value;
    /// <inheritdoc/>
    public IActorService ActorService => _actorService.Value;
    /// <inheritdoc/>
    public IEventProjector<FuturesOptionTickDataCommandActor> EventProjector => _eventProjector.Value;
    /// <inheritdoc/>
    public ISequenceIdGenerator SequenceIdGenerator => _sequenceIdGenerator.Value;
    Lazy<T> ResolveOnce<T>() where T : class => new(() => IsArgumentNull.Set(Container.Resolve<T>())!);
}

