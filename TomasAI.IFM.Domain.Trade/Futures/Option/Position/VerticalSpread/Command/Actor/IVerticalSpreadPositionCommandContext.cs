using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Actor;
public interface IVerticalSpreadPositionCommandContext : ICommandActorContext<FuturesVerticalSpreadTradePositionCommandActor>
{
    IDbContextFactory DbFactory { get; }

    IEventSourceActorDbContext DbEventSource { get; }

    IDurableReplayQueue DurableReplayQueue { get; }

    IBlackboardService BlackboardService { get; }

    IActorService ActorService { get; }

    IEventProjector<FuturesVerticalSpreadTradePositionCommandActor> EventProjector { get; }

    IResidentEventSourceActorStateRepository<VerticalSpreadPositionCommandState> StateRepository { get; }

    ILogger<FuturesVerticalSpreadTradePositionCommandActor> Logger { get; }
}
