using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.State;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Actor;

/// <summary>Provides the closed-generic strategy-configuration Command context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class ParameterStartupCommandContext
    : CommandActorContext,
      IParameterStartupCommandContext
{
    readonly Lazy<IParameterAccessPolicy> accessPolicy;
    public IParameterAccessPolicy AccessPolicy => accessPolicy.Value;

    readonly Lazy<IEventSourceActorDbContext> dbEventSource;
    readonly Lazy<IConfigurationDbContext> configurationDb;
    readonly Lazy<IEventProjector<ParameterStartupCommandActor>> projector;
    readonly Lazy<IEventSourceActorStateRepository<ParameterStartupCommandState>> repository;
    readonly Lazy<IDurableReplayQueue> queue;
    readonly Lazy<IEventSourceActorStateFactory> stateFactory;
    readonly Lazy<IActorService> actorService;

    /// <summary>Initializes the context.</summary>
    public ParameterStartupCommandContext(
        IActorSupervisor supervisor,
        ILogger<ParameterStartupCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, ParameterStartupCommandActor.ActorName))
    {

        Logger = IsArgumentNull.Set(logger);
        accessPolicy = new(() => IsArgumentNull.Set(Container.Resolve<IParameterAccessPolicy>())!);
        dbEventSource = ResolveOnce<IEventSourceActorDbContext>();
        configurationDb = ResolveOnce<IConfigurationDbContext>();
        projector = ResolveOnce<IEventProjector<ParameterStartupCommandActor>>();
        repository = ResolveOnce<IEventSourceActorStateRepository<ParameterStartupCommandState>>();
        queue = ResolveOnce<IDurableReplayQueue>();
        stateFactory = ResolveOnce<IEventSourceActorStateFactory>();
        actorService = ResolveOnce<IActorService>();
    }

    /// <inheritdoc />
    public IEventSourceActorDbContext DbEventSource => dbEventSource.Value;
    /// <inheritdoc />
    public IConfigurationDbContext ConfigurationDb => configurationDb.Value;
    /// <inheritdoc />
    public IEventProjector<ParameterStartupCommandActor> EventProjector => projector.Value;
    /// <inheritdoc />
    public IEventSourceActorStateRepository<ParameterStartupCommandState> StateRepository => repository.Value;
    /// <inheritdoc />
    public IDurableReplayQueue DurableReplayQueue => queue.Value;
    /// <inheritdoc />

    /// <inheritdoc />
    public IEventSourceActorStateFactory StateFactory => stateFactory.Value;
    /// <inheritdoc />
    public IActorService ActorService => actorService.Value;
    /// <inheritdoc />
    public ILogger<ParameterStartupCommandActor> Logger { get; }

    Lazy<T> ResolveOnce<T>() where T : class => new(() => IsArgumentNull.Set(Container.Resolve<T>())!);
}
