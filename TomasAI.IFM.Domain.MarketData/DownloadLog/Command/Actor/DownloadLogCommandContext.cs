using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Actor;

/// <summary>
/// Provides the shared actor runtime context and DownloadLog-specific services required by <see cref="DownloadLogCommandActor"/>.
/// </summary>
/// <remarks>
/// The supervisor and mailbox identifier are passed to <see cref="CommandActorContext"/>. The remaining constructor
/// dependencies implement <see cref="IDownloadLogCommandContext"/> and are exposed to DownloadLog command processing.
/// </remarks>
public sealed class DownloadLogCommandContext :
    CommandActorContext,
    ICommandActorContext<DownloadLogCommandActor>,
    IDownloadLogCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> _dbEventSource;
    readonly Lazy<IDurableReplayQueue> _durableReplayQueue;
    readonly Lazy<IEventSourceActorStateFactory> _stateFactory;
    readonly Lazy<IActorService> _actorService;
    readonly Lazy<IEventProjector<DownloadLogCommandActor>> _eventProjector;

    /// <summary>
    /// Initializes a DownloadLog command context.
    /// </summary>
    /// <param name="supervisor">The actor supervisor that owns the DownloadLog command actor.</param>
    /// <param name="dbFactory">The database-context factory used by DownloadLog command processing.</param>
    /// <param name="blackboardService">The blackboard service used by DownloadLog command processing.</param>
    /// <param name="logger">The logger associated with <see cref="DownloadLogCommandActor"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="supervisor"/>, <paramref name="dbFactory"/>, <paramref name="blackboardService"/>, or
    /// <paramref name="logger"/> is <see langword="null"/>.
    /// </exception>
    public DownloadLogCommandContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        IBlackboardService blackboardService,
        ILogger<DownloadLogCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, DownloadLogCommandActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        BlackboardService = IsArgumentNull.Set(blackboardService);
        Logger = IsArgumentNull.Set(logger);
        _dbEventSource = ResolveOnce<IEventSourceActorDbContext>();
        _durableReplayQueue = ResolveOnce<IDurableReplayQueue>();
        _stateFactory = ResolveOnce<IEventSourceActorStateFactory>();
        _actorService = ResolveOnce<IActorService>();
        _eventProjector = ResolveOnce<IEventProjector<DownloadLogCommandActor>>();
    }

    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }

    /// <inheritdoc/>
    public IBlackboardService BlackboardService { get; }

    /// <inheritdoc/>
    public ILogger<DownloadLogCommandActor> Logger { get; }

    /// <inheritdoc/>
    public IEventSourceActorDbContext DbEventSource => _dbEventSource.Value;

    /// <inheritdoc/>
    public IDurableReplayQueue DurableReplayQueue => _durableReplayQueue.Value;

    /// <inheritdoc/>
    public IEventSourceActorStateFactory StateFactory => _stateFactory.Value;

    /// <inheritdoc/>
    public IActorService ActorService => _actorService.Value;

    /// <inheritdoc/>
    public IEventProjector<DownloadLogCommandActor> EventProjector => _eventProjector.Value;

    Lazy<TService> ResolveOnce<TService>()
        where TService : class
        => new(() => IsArgumentNull.Set(Container.Resolve<TService>())!);
}
