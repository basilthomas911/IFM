using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.Actor;
public sealed class FuturesExitPositionWorkflowCommandContext : CommandActorContext, IFuturesExitPositionWorkflowCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> eventSource;
    readonly Lazy<IDurableReplayQueue> replay;
    readonly Lazy<IActorService> actors;
    readonly Lazy<IEventProjector<FuturesExitPositionWorkflowCommandActor>> projector;
    readonly Lazy<IEventSourceActorStateRepository<FuturesExitPositionWorkflowCommandState>> repository;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public FuturesExitPositionWorkflowCommandContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, IBlackboardService blackboard, ILogger<FuturesExitPositionWorkflowCommandActor> logger) : base(supervisor, new(ActorType.Command, FuturesExitPositionWorkflowCommandActor.ActorName))
    {
        DbFactory = dbFactory;
        BlackboardService = blackboard;
        Logger = logger;
        eventSource = Resolve<IEventSourceActorDbContext>();
        replay = Resolve<IDurableReplayQueue>();
        actors = Resolve<IActorService>();
        projector = Resolve<IEventProjector<FuturesExitPositionWorkflowCommandActor>>();
        repository = Resolve<IEventSourceActorStateRepository<FuturesExitPositionWorkflowCommandState>>();
    }

    public IDbContextFactory DbFactory { get; }
    public IBlackboardService BlackboardService { get; }
    public ILogger<FuturesExitPositionWorkflowCommandActor> Logger { get; }
    public IEventSourceActorDbContext DbEventSource => eventSource.Value;
    public IDurableReplayQueue DurableReplayQueue => replay.Value;
    public IActorService ActorService => actors.Value;
    public IEventProjector<FuturesExitPositionWorkflowCommandActor> EventProjector => projector.Value;
    public IEventSourceActorStateRepository<FuturesExitPositionWorkflowCommandState> StateRepository => repository.Value;

    Lazy<T> Resolve<T>()
        where T : class => new(() => Container.Resolve<T>());
}
