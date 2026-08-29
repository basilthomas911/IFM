using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Query.Actor;

public interface IMarketConditionQueryContext : IQueryActorContext<MarketConditionQueryActor>
{
    IDbContextFactory DbFactory { get; }
    ILogger<MarketConditionQueryActor> Logger { get; }
}
public sealed class MarketConditionQueryContext : QueryActorContext,
    IQueryActorContext<MarketConditionQueryActor>, IMarketConditionQueryContext
{
    public MarketConditionQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory,
        ILogger<MarketConditionQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, MarketConditionQueryActor.ActorName))
    { DbFactory = IsArgumentNull.Set(dbFactory); Logger = IsArgumentNull.Set(logger); }
    public IDbContextFactory DbFactory { get; }
    public ILogger<MarketConditionQueryActor> Logger { get; }
}
