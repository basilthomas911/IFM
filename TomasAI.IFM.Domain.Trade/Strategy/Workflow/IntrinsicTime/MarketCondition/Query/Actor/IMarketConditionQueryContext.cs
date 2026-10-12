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
