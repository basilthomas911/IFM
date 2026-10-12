using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.Actor;
public interface IFuturesExitPositionWorkflowCommandContext : ICommandActorContext<FuturesExitPositionWorkflowCommandActor>
{
    IDbContextFactory DbFactory { get; }

    IEventSourceActorDbContext DbEventSource { get; }

    IDurableReplayQueue DurableReplayQueue { get; }

    IBlackboardService BlackboardService { get; }

    IActorService ActorService { get; }

    IEventProjector<FuturesExitPositionWorkflowCommandActor> EventProjector { get; }

    IEventSourceActorStateRepository<FuturesExitPositionWorkflowCommandState> StateRepository { get; }

    ILogger<FuturesExitPositionWorkflowCommandActor> Logger { get; }
}
