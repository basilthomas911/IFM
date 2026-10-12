using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.Actor;
public interface IVerticalSpreadExitPositionWorkflowCommandContext : ICommandActorContext<VerticalSpreadExitPositionWorkflowCommandActor>
{
    IDbContextFactory DbFactory { get; }

    IEventSourceActorDbContext DbEventSource { get; }

    IDurableReplayQueue DurableReplayQueue { get; }

    IBlackboardService BlackboardService { get; }

    IActorService ActorService { get; }

    IEventProjector<VerticalSpreadExitPositionWorkflowCommandActor> EventProjector { get; }

    IEventSourceActorStateRepository<VerticalSpreadExitPositionWorkflowCommandState> StateRepository { get; }

    ILogger<VerticalSpreadExitPositionWorkflowCommandActor> Logger { get; }
}
