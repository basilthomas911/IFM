using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query.Actor;
public sealed class TradeSelectionQueryContext : QueryActorContext, ITradeSelectionQueryContext
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public TradeSelectionQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<TradeSelectionQueryActor> logger) : base(supervisor, new ActorMailboxId(ActorType.Query, TradeSelectionQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    public IPortfolioQueryApi PortfolioQueries => Container.Resolve<IPortfolioQueryApi>();
    public IDbContextFactory DbFactory { get; }
    public ILogger<TradeSelectionQueryActor> Logger { get; }
}
