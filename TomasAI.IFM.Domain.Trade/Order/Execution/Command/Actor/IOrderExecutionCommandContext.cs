using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command.Actor;
/// <summary>Exposes the services used by OrderExecution command handlers and projections.</summary>
public interface IOrderExecutionCommandContext : ICommandActorContext<OrderExecutionCommandActor>
{
    IDbContextFactory DbFactory { get; }

    IEventSourceActorDbContext DbEventSource { get; }

    IDurableReplayQueue DurableReplayQueue { get; }

    IBlackboardService BlackboardService { get; }

    IActorService ActorService { get; }

    IPortfolioTradeAccountingApi PortfolioAccounting { get; }

    IEventProjector<OrderExecutionCommandActor> EventProjector { get; }

    IEventSourceActorStateRepository<OrderExecutionCommandState> StateRepository { get; }

    ILogger<OrderExecutionCommandActor> Logger { get; }
}
