using TomasAI.IFM.Application.Contracts.Portfolio;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query.Actor;
public sealed class RiskQueryContext : QueryActorContext, IRiskQueryContext
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public RiskQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<RiskQueryActor> logger, TimeProvider? timeProvider = null) : base(supervisor, new ActorMailboxId(ActorType.Query, RiskQueryActor.ActorName))
    {
        TimeProvider = timeProvider ?? TimeProvider.System;
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    public TimeProvider TimeProvider { get; }
    public IPortfolioQueryApi PortfolioQueries => Container.Resolve<IPortfolioQueryApi>();
    public IPortfolioFundOrderReader Funds => Container.Resolve<IPortfolioFundOrderReader>();
    public IFinancialQueryStore Financial => Container.Resolve<IFinancialQueryStore>();
    public IDbContextFactory DbFactory { get; }
    public ILogger<RiskQueryActor> Logger { get; }
}
