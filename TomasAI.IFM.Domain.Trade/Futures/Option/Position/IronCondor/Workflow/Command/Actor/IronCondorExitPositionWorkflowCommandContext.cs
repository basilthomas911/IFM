using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Command.Actor;

public interface IIronCondorExitPositionWorkflowCommandContext :
    ICommandActorContext<IronCondorExitPositionWorkflowCommandActor>
{
    IDbContextFactory DbFactory { get; }
    IEventSourceActorDbContext DbEventSource { get; }
    IDurableReplayQueue DurableReplayQueue { get; }
    IBlackboardService BlackboardService { get; }
    IActorService ActorService { get; }
    IEventProjector<IronCondorExitPositionWorkflowCommandActor> EventProjector { get; }
    IEventSourceActorStateRepository<IronCondorExitPositionWorkflowCommandState> StateRepository { get; }
    ILogger<IronCondorExitPositionWorkflowCommandActor> Logger { get; }
}

public sealed class IronCondorExitPositionWorkflowCommandContext : CommandActorContext,
    ICommandActorContext<IronCondorExitPositionWorkflowCommandActor>,
    IIronCondorExitPositionWorkflowCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> eventSource;
    readonly Lazy<IDurableReplayQueue> replay;
    readonly Lazy<IActorService> actors;
    readonly Lazy<IEventProjector<IronCondorExitPositionWorkflowCommandActor>> projector;
    readonly Lazy<IEventSourceActorStateRepository<IronCondorExitPositionWorkflowCommandState>> repository;

    public IronCondorExitPositionWorkflowCommandContext(IActorSupervisor supervisor,
        IDbContextFactory dbFactory, IBlackboardService blackboard,
        ILogger<IronCondorExitPositionWorkflowCommandActor> logger)
        : base(supervisor, new(ActorType.Command, IronCondorExitPositionWorkflowCommandActor.ActorName))
    {
        DbFactory = dbFactory;
        BlackboardService = blackboard;
        Logger = logger;
        eventSource = Resolve<IEventSourceActorDbContext>();
        replay = Resolve<IDurableReplayQueue>();
        actors = Resolve<IActorService>();
        projector = Resolve<IEventProjector<IronCondorExitPositionWorkflowCommandActor>>();
        repository = Resolve<IEventSourceActorStateRepository<IronCondorExitPositionWorkflowCommandState>>();
    }

    public IDbContextFactory DbFactory { get; }
    public IBlackboardService BlackboardService { get; }
    public ILogger<IronCondorExitPositionWorkflowCommandActor> Logger { get; }
    public IEventSourceActorDbContext DbEventSource => eventSource.Value;
    public IDurableReplayQueue DurableReplayQueue => replay.Value;
    public IActorService ActorService => actors.Value;
    public IEventProjector<IronCondorExitPositionWorkflowCommandActor> EventProjector => projector.Value;
    public IEventSourceActorStateRepository<IronCondorExitPositionWorkflowCommandState> StateRepository => repository.Value;
    Lazy<T> Resolve<T>() where T : class => new(() => Container.Resolve<T>());
}
