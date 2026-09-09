using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
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
    IPortfolioEventStore Funds { get; }
    IFinancialQueryStore Financial { get; }
    IDbContextFactory DbFactory { get; }
    IPortfolioQueryApi PortfolioQueries {get;}
    ILogger<RiskQueryActor> Logger { get; }
}
public sealed class RiskQueryContext : QueryActorContext,
    IQueryActorContext<RiskQueryActor>, IRiskQueryContext
{
    public RiskQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory,
        ILogger<RiskQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, RiskQueryActor.ActorName))
    { DbFactory = IsArgumentNull.Set(dbFactory); Logger = IsArgumentNull.Set(logger); }
    public IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState> WorkflowRepository => Container.Resolve<IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>>();
    public IPortfolioQueryApi PortfolioQueries=>Container.Resolve<IPortfolioQueryApi>();
    public RiskHistoryJournal Journal => Container.Resolve<RiskHistoryJournal>();
    public IPortfolioEventStore Funds => Container.Resolve<IPortfolioEventStore>();
    public IFinancialQueryStore Financial => Container.Resolve<IFinancialQueryStore>();
    public IDbContextFactory DbFactory { get; }
    public ILogger<RiskQueryActor> Logger { get; }
}
