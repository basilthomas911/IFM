using TomasAI.IFM.Application.Contracts.Portfolio;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query.Actor;
public interface IRiskQueryContext : IQueryActorContext<RiskQueryActor>
{
    IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState> WorkflowRepository { get; }

    RiskHistoryJournal Journal { get; }

    IPortfolioFundOrderReader Funds { get; }

    IFinancialQueryStore Financial { get; }

    IDbContextFactory DbFactory { get; }

    IPortfolioQueryApi PortfolioQueries { get; }

    ILogger<RiskQueryActor> Logger { get; }
}
