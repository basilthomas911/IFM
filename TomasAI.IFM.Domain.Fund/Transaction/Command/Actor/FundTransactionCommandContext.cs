using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Fund.Transaction.Command.Actor;

/// <summary>
/// Provides the shared command runtime context and Fund transaction services required by
/// <see cref="FundTransactionCommandActor"/>.
/// </summary>
public sealed class FundTransactionCommandContext :
    CommandActorContext,
    ICommandActorContext<FundTransactionCommandActor>,
    IFundTransactionCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> _dbEventSource;
    readonly Lazy<IDurableReplayQueue> _durableReplayQueue;
    readonly Lazy<IEventSourceActorStateFactory> _stateFactory;
    readonly Lazy<IActorService> _actorService;
    readonly Lazy<IEventProjector<FundTransactionCommandActor>> _eventProjector;

    /// <summary>Initializes a Fund transaction command context.</summary>
    /// <param name="supervisor">The actor supervisor that owns the command actor.</param>
    /// <param name="dbFactory">The database-context factory used by Fund transaction processing.</param>
    /// <param name="blackboardService">The blackboard service used by event projection.</param>
    /// <param name="logger">The logger associated with the command actor.</param>
    public FundTransactionCommandContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        IBlackboardService blackboardService,
        ILogger<FundTransactionCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, FundTransactionCommandActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        BlackboardService = IsArgumentNull.Set(blackboardService);
        Logger = IsArgumentNull.Set(logger);
        _dbEventSource = ResolveOnce<IEventSourceActorDbContext>();
        _durableReplayQueue = ResolveOnce<IDurableReplayQueue>();
        _stateFactory = ResolveOnce<IEventSourceActorStateFactory>();
        _actorService = ResolveOnce<IActorService>();
        _eventProjector = ResolveOnce<IEventProjector<FundTransactionCommandActor>>();
    }

    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }

    /// <inheritdoc/>
    public IBlackboardService BlackboardService { get; }

    /// <inheritdoc/>
    public ILogger<FundTransactionCommandActor> Logger { get; }

    /// <inheritdoc/>
    public IEventSourceActorDbContext DbEventSource => _dbEventSource.Value;

    /// <inheritdoc/>
    public IDurableReplayQueue DurableReplayQueue => _durableReplayQueue.Value;

    /// <inheritdoc/>
    public IEventSourceActorStateFactory StateFactory => _stateFactory.Value;

    /// <inheritdoc/>
    public IActorService ActorService => _actorService.Value;

    /// <inheritdoc/>
    public IEventProjector<FundTransactionCommandActor> EventProjector => _eventProjector.Value;

    Lazy<TService> ResolveOnce<TService>()
        where TService : class
        => new(() => IsArgumentNull.Set(Container.Resolve<TService>())!);
}
