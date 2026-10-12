using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Command.Actor;
public interface IFuturesPositionCommandContext : ICommandActorContext<FuturesTradePositionCommandActor>
{
    IDbContextFactory DbFactory { get; }

    IBlackboardService BlackboardService { get; }

    ILogger<FuturesTradePositionCommandActor> Logger { get; }

    IEventSourceActorDbContext DbEventSource { get; }

    IDurableReplayQueue DurableReplayQueue { get; }

    IActorService ActorService { get; }

    IEventProjector<FuturesTradePositionCommandActor> EventProjector { get; }

    IResidentEventSourceActorStateRepository<FuturesPositionCommandState> StateRepository { get; }
}
