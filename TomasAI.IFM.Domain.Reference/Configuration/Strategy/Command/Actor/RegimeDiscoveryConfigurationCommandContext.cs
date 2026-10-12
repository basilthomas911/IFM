using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Actor;

/// <summary>Provides the closed-generic strategy-configuration Command context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class RegimeDiscoveryConfigurationCommandContext
    : CommandActorContext,
      IRegimeDiscoveryConfigurationCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> dbEventSource;
    readonly Lazy<IConfigurationDbContext> configurationDb;
    readonly Lazy<IEventProjector<RegimeDiscoveryConfigurationCommandActor>> projector;
    readonly Lazy<IEventSourceActorStateRepository<RegimeDiscoveryConfigurationCommandState>> repository;
    readonly Lazy<IDurableReplayQueue> queue;
    readonly Lazy<IEventSourceActorStateFactory> stateFactory;
    readonly Lazy<IActorService> actorService;

    /// <summary>Initializes the context.</summary>
    public RegimeDiscoveryConfigurationCommandContext(
        IActorSupervisor supervisor,
        IBlackboardService blackboardService,
        ILogger<RegimeDiscoveryConfigurationCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, RegimeDiscoveryConfigurationCommandActor.ActorName))
    {
        BlackboardService = IsArgumentNull.Set(blackboardService);
        Logger = IsArgumentNull.Set(logger);
        dbEventSource = ResolveOnce<IEventSourceActorDbContext>();
        configurationDb = ResolveOnce<IConfigurationDbContext>();
        projector = ResolveOnce<IEventProjector<RegimeDiscoveryConfigurationCommandActor>>();
        repository = ResolveOnce<IEventSourceActorStateRepository<RegimeDiscoveryConfigurationCommandState>>();
        queue = ResolveOnce<IDurableReplayQueue>();
        stateFactory = ResolveOnce<IEventSourceActorStateFactory>();
        actorService = ResolveOnce<IActorService>();
    }

    /// <inheritdoc />
    public IEventSourceActorDbContext DbEventSource => dbEventSource.Value;
    /// <inheritdoc />
    public IConfigurationDbContext ConfigurationDb => configurationDb.Value;
    /// <inheritdoc />
    public IEventProjector<RegimeDiscoveryConfigurationCommandActor> EventProjector => projector.Value;
    /// <inheritdoc />
    public IEventSourceActorStateRepository<RegimeDiscoveryConfigurationCommandState> StateRepository => repository.Value;
    /// <inheritdoc />
    public IDurableReplayQueue DurableReplayQueue => queue.Value;
    /// <inheritdoc />
    public IBlackboardService BlackboardService { get; }
    /// <inheritdoc />
    public IEventSourceActorStateFactory StateFactory => stateFactory.Value;
    /// <inheritdoc />
    public IActorService ActorService => actorService.Value;
    /// <inheritdoc />
    public ILogger<RegimeDiscoveryConfigurationCommandActor> Logger { get; }

    Lazy<T> ResolveOnce<T>() where T : class => new(() => IsArgumentNull.Set(Container.Resolve<T>())!);
}
