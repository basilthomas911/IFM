using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Fund.Command.Actor;

/// <summary>
/// Provides the shared actor runtime context and Fund-specific services required by <see cref="FundCommandActor"/>.
/// </summary>
/// <remarks>
/// The supervisor and mailbox identifier are passed to <see cref="CommandActorContext"/>. The remaining constructor
/// dependencies implement <see cref="IFundCommandContext"/> and are exposed to Fund command processing.
/// </remarks>
public sealed class FundCommandContext :
    CommandActorContext,
    ICommandActorContext<FundCommandActor>,
    IFundCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> _dbEventSource;
    readonly Lazy<IDurableReplayQueue> _durableReplayQueue;
    readonly Lazy<IEventSourceActorStateFactory> _stateFactory;
    readonly Lazy<IActorService> _actorService;
    readonly Lazy<IEventProjector<FundCommandActor>> _eventProjector;

    /// <summary>
    /// Initializes a Fund command context.
    /// </summary>
    /// <param name="supervisor">The actor supervisor that owns the Fund command actor.</param>
    /// <param name="dbFactory">The database-context factory used by Fund command processing.</param>
    /// <param name="blackboardService">The blackboard service used by Fund command processing.</param>
    /// <param name="logger">The logger associated with <see cref="FundCommandActor"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="supervisor"/>, <paramref name="dbFactory"/>, <paramref name="blackboardService"/>, or
    /// <paramref name="logger"/> is <see langword="null"/>.
    /// </exception>
    public FundCommandContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        IBlackboardService blackboardService,
        ILogger<FundCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, FundCommandActor.Actor))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        BlackboardService = IsArgumentNull.Set(blackboardService);
        Logger = IsArgumentNull.Set(logger);
        _dbEventSource = ResolveOnce<IEventSourceActorDbContext>();
        _durableReplayQueue = ResolveOnce<IDurableReplayQueue>();
        _stateFactory = ResolveOnce<IEventSourceActorStateFactory>();
        _actorService = ResolveOnce<IActorService>();
        _eventProjector = ResolveOnce<IEventProjector<FundCommandActor>>();
    }

    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }

    /// <inheritdoc/>
    public IBlackboardService BlackboardService { get; }

    /// <inheritdoc/>
    public ILogger<FundCommandActor> Logger { get; }

    /// <inheritdoc/>
    public IEventSourceActorDbContext DbEventSource => _dbEventSource.Value;

    /// <inheritdoc/>
    public IDurableReplayQueue DurableReplayQueue => _durableReplayQueue.Value;

    /// <inheritdoc/>
    public IEventSourceActorStateFactory StateFactory => _stateFactory.Value;

    /// <inheritdoc/>
    public IActorService ActorService => _actorService.Value;

    /// <inheritdoc/>
    public IEventProjector<FundCommandActor> EventProjector => _eventProjector.Value;

    Lazy<TService> ResolveOnce<TService>()
        where TService : class
        => new(() => IsArgumentNull.Set(Container.Resolve<TService>())!);
}
