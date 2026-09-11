using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;

/// <summary>Defines readonly dependencies owned by the strategy-configuration Command actor.</summary>
public interface IParameterAssignmentCommandContext
    : ICommandActorContext<ParameterAssignmentCommandActor>
{
    IParameterAccessPolicy AccessPolicy { get; }
    /// <summary>Gets EventSourceDb.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets ConfigurationDb.</summary>
    IConfigurationDbContext ConfigurationDb { get; }
    /// <summary>Gets the event projector.</summary>
    IEventProjector<ParameterAssignmentCommandActor> EventProjector { get; }
    /// <summary>Gets the state repository.</summary>
    IEventSourceActorStateRepository<ParameterAssignmentCommandState> StateRepository { get; }
    /// <summary>Gets the projector queue dependency.</summary>
    IDurableReplayQueue DurableReplayQueue { get; }
    /// <summary>Gets the blackboard dependency.</summary>
    IBlackboardService BlackboardService { get; }
    /// <summary>Gets the state factory.</summary>
    IEventSourceActorStateFactory StateFactory { get; }
    /// <summary>Gets actor infrastructure.</summary>
    IActorService ActorService { get; }
    IEventSourceActorStateRepository<ParameterSetCommandState> ParameterSets {get;}
    /// <summary>Gets the logger.</summary>
    ILogger<ParameterAssignmentCommandActor> Logger { get; }
}

/// <summary>Provides the closed-generic strategy-configuration Command context.</summary>
public sealed class ParameterAssignmentCommandContext
    : CommandActorContext,
      ICommandActorContext<ParameterAssignmentCommandActor>,
      IParameterAssignmentCommandContext
{
    readonly Lazy<IParameterAccessPolicy> accessPolicy;
    public IParameterAccessPolicy AccessPolicy => accessPolicy.Value;

    readonly Lazy<IEventSourceActorStateRepository<ParameterSetCommandState>> parameterSets;
    readonly Lazy<IEventSourceActorDbContext> dbEventSource;
    readonly Lazy<IConfigurationDbContext> configurationDb;
    readonly Lazy<IEventProjector<ParameterAssignmentCommandActor>> projector;
    readonly Lazy<IEventSourceActorStateRepository<ParameterAssignmentCommandState>> repository;
    readonly Lazy<IDurableReplayQueue> queue;
    readonly Lazy<IEventSourceActorStateFactory> stateFactory;
    readonly Lazy<IActorService> actorService;

    /// <summary>Initializes the context.</summary>
    public ParameterAssignmentCommandContext(
        IActorSupervisor supervisor,
        IBlackboardService blackboardService,
        ILogger<ParameterAssignmentCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, ParameterAssignmentCommandActor.ActorName))
    {
        BlackboardService = IsArgumentNull.Set(blackboardService);
        Logger = IsArgumentNull.Set(logger);
        accessPolicy = new(() => IsArgumentNull.Set(Container.Resolve<IParameterAccessPolicy>())!);
        parameterSets = ResolveOnce<IEventSourceActorStateRepository<ParameterSetCommandState>>();
        dbEventSource = ResolveOnce<IEventSourceActorDbContext>();
        configurationDb = ResolveOnce<IConfigurationDbContext>();
        projector = ResolveOnce<IEventProjector<ParameterAssignmentCommandActor>>();
        repository = ResolveOnce<IEventSourceActorStateRepository<ParameterAssignmentCommandState>>();
        queue = ResolveOnce<IDurableReplayQueue>();
        stateFactory = ResolveOnce<IEventSourceActorStateFactory>();
        actorService = ResolveOnce<IActorService>();
    }

    /// <inheritdoc />
    public IEventSourceActorStateRepository<ParameterSetCommandState> ParameterSets => parameterSets.Value;
    public IEventSourceActorDbContext DbEventSource => dbEventSource.Value;
    /// <inheritdoc />
    public IConfigurationDbContext ConfigurationDb => configurationDb.Value;
    /// <inheritdoc />
    public IEventProjector<ParameterAssignmentCommandActor> EventProjector => projector.Value;
    /// <inheritdoc />
    public IEventSourceActorStateRepository<ParameterAssignmentCommandState> StateRepository => repository.Value;
    /// <inheritdoc />
    public IDurableReplayQueue DurableReplayQueue => queue.Value;
    /// <inheritdoc />
    public IBlackboardService BlackboardService { get; }
    /// <inheritdoc />
    public IEventSourceActorStateFactory StateFactory => stateFactory.Value;
    /// <inheritdoc />
    public IActorService ActorService => actorService.Value;
    /// <inheritdoc />
    public ILogger<ParameterAssignmentCommandActor> Logger { get; }

    Lazy<T> ResolveOnce<T>() where T : class => new(() => IsArgumentNull.Set(Container.Resolve<T>())!);
}
