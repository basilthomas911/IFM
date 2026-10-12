using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Command.Actor;
public interface IFuturesTradeCommandContext : ICommandActorContext<FuturesTradeCommandActor>
{
    IDbContextFactory DbFactory { get; }

    IEventSourceActorDbContext DbEventSource { get; }

    IDurableReplayQueue DurableReplayQueue { get; }

    IBlackboardService BlackboardService { get; }

    IActorService ActorService { get; }

    IEventProjector<FuturesTradeCommandActor> EventProjector { get; }

    IEventSourceActorStateRepository<FuturesTradeCommandState> StateRepository { get; }

    ILogger<FuturesTradeCommandActor> Logger { get; }
}
