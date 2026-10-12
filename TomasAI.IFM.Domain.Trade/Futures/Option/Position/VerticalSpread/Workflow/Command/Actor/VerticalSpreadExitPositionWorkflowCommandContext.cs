using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.Actor;
public sealed class VerticalSpreadExitPositionWorkflowCommandContext : CommandActorContext, IVerticalSpreadExitPositionWorkflowCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> eventSource;
    readonly Lazy<IDurableReplayQueue> replay;
    readonly Lazy<IActorService> actors;
    readonly Lazy<IEventProjector<VerticalSpreadExitPositionWorkflowCommandActor>> projector;
    readonly Lazy<IEventSourceActorStateRepository<VerticalSpreadExitPositionWorkflowCommandState>> repository;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public VerticalSpreadExitPositionWorkflowCommandContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, IBlackboardService blackboard, ILogger<VerticalSpreadExitPositionWorkflowCommandActor> logger) : base(supervisor, new(ActorType.Command, VerticalSpreadExitPositionWorkflowCommandActor.ActorName))
    {
        DbFactory = dbFactory;
        BlackboardService = blackboard;
        Logger = logger;
        eventSource = Resolve<IEventSourceActorDbContext>();
        replay = Resolve<IDurableReplayQueue>();
        actors = Resolve<IActorService>();
        projector = Resolve<IEventProjector<VerticalSpreadExitPositionWorkflowCommandActor>>();
        repository = Resolve<IEventSourceActorStateRepository<VerticalSpreadExitPositionWorkflowCommandState>>();
    }

    public IDbContextFactory DbFactory { get; }
    public IBlackboardService BlackboardService { get; }
    public ILogger<VerticalSpreadExitPositionWorkflowCommandActor> Logger { get; }
    public IEventSourceActorDbContext DbEventSource => eventSource.Value;
    public IDurableReplayQueue DurableReplayQueue => replay.Value;
    public IActorService ActorService => actors.Value;
    public IEventProjector<VerticalSpreadExitPositionWorkflowCommandActor> EventProjector => projector.Value;
    public IEventSourceActorStateRepository<VerticalSpreadExitPositionWorkflowCommandState> StateRepository => repository.Value;

    Lazy<T> Resolve<T>()
        where T : class => new(() => Container.Resolve<T>());
}
