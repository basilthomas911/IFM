using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Query.Actor;
public sealed class MarketConditionQueryContext : QueryActorContext, IMarketConditionQueryContext
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public MarketConditionQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<MarketConditionQueryActor> logger) : base(supervisor, new ActorMailboxId(ActorType.Query, MarketConditionQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    public IDbContextFactory DbFactory { get; }
    public ILogger<MarketConditionQueryActor> Logger { get; }
}
