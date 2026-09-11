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
public interface IParameterSetCommandContext
    : ICommandActorContext<ParameterSetCommandActor>
{
    IParameterAccessPolicy AccessPolicy { get; }
    IEventSourceActorStateRepository<ParameterStartupCommandState> Startups {get;}
    /// <summary>Gets EventSourceDb.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets ConfigurationDb.</summary>
    IConfigurationDbContext ConfigurationDb { get; }
    /// <summary>Gets the event projector.</summary>
    IEventProjector<ParameterSetCommandActor> EventProjector { get; }
    /// <summary>Gets the state repository.</summary>
    IEventSourceActorStateRepository<ParameterSetCommandState> StateRepository { get; }
    /// <summary>Gets the projector queue dependency.</summary>
    IDurableReplayQueue DurableReplayQueue { get; }
    /// <summary>Gets the blackboard dependency.</summary>
    IBlackboardService BlackboardService { get; }
    /// <summary>Gets the state factory.</summary>
    IEventSourceActorStateFactory StateFactory { get; }
    /// <summary>Gets actor infrastructure.</summary>
    IActorService ActorService { get; }
    IEventSourceActorStateRepository<ParameterAssignmentCommandState> Assignments {get;}
    /// <summary>Gets the logger.</summary>
    ILogger<ParameterSetCommandActor> Logger { get; }
}

/// <summary>Provides the closed-generic strategy-configuration Command context.</summary>
public sealed class ParameterSetCommandContext
    : CommandActorContext,
      ICommandActorContext<ParameterSetCommandActor>,
      IParameterSetCommandContext
{
    readonly Lazy<IEventSourceActorStateRepository<ParameterStartupCommandState>> startups;
    public IEventSourceActorStateRepository<ParameterStartupCommandState> Startups=>startups.Value;
    readonly Lazy<IParameterAccessPolicy> accessPolicy;
    public IParameterAccessPolicy AccessPolicy => accessPolicy.Value;

    readonly Lazy<IEventSourceActorStateRepository<ParameterAssignmentCommandState>> assignments;
    readonly Lazy<IEventSourceActorDbContext> dbEventSource;
    readonly Lazy<IConfigurationDbContext> configurationDb;
    readonly Lazy<IEventProjector<ParameterSetCommandActor>> projector;
    readonly Lazy<IEventSourceActorStateRepository<ParameterSetCommandState>> repository;
    readonly Lazy<IDurableReplayQueue> queue;
    readonly Lazy<IEventSourceActorStateFactory> stateFactory;
    readonly Lazy<IActorService> actorService;

    /// <summary>Initializes the context.</summary>
    public ParameterSetCommandContext(
        IActorSupervisor supervisor,
        IBlackboardService blackboardService,
        ILogger<ParameterSetCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, ParameterSetCommandActor.ActorName))
    {
        BlackboardService = IsArgumentNull.Set(blackboardService);
        Logger = IsArgumentNull.Set(logger);
        startups = new(()=>IsArgumentNull.Set(Container.Resolve<IEventSourceActorStateRepository<ParameterStartupCommandState>>())!);
        accessPolicy = new(() => IsArgumentNull.Set(Container.Resolve<IParameterAccessPolicy>())!);
        assignments = ResolveOnce<IEventSourceActorStateRepository<ParameterAssignmentCommandState>>();
        dbEventSource = ResolveOnce<IEventSourceActorDbContext>();
        configurationDb = ResolveOnce<IConfigurationDbContext>();
        projector = ResolveOnce<IEventProjector<ParameterSetCommandActor>>();
        repository = ResolveOnce<IEventSourceActorStateRepository<ParameterSetCommandState>>();
        queue = ResolveOnce<IDurableReplayQueue>();
        stateFactory = ResolveOnce<IEventSourceActorStateFactory>();
        actorService = ResolveOnce<IActorService>();
    }

    /// <inheritdoc />
    public IEventSourceActorStateRepository<ParameterAssignmentCommandState> Assignments => assignments.Value;
    public IEventSourceActorDbContext DbEventSource => dbEventSource.Value;
    /// <inheritdoc />
    public IConfigurationDbContext ConfigurationDb => configurationDb.Value;
    /// <inheritdoc />
    public IEventProjector<ParameterSetCommandActor> EventProjector => projector.Value;
    /// <inheritdoc />
    public IEventSourceActorStateRepository<ParameterSetCommandState> StateRepository => repository.Value;
    /// <inheritdoc />
    public IDurableReplayQueue DurableReplayQueue => queue.Value;
    /// <inheritdoc />
    public IBlackboardService BlackboardService { get; }
    /// <inheritdoc />
    public IEventSourceActorStateFactory StateFactory => stateFactory.Value;
    /// <inheritdoc />
    public IActorService ActorService => actorService.Value;
    /// <inheritdoc />
    public ILogger<ParameterSetCommandActor> Logger { get; }

    Lazy<T> ResolveOnce<T>() where T : class => new(() => IsArgumentNull.Set(Container.Resolve<T>())!);
}
