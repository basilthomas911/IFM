using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Query.Actor;
public interface IStrategyTradePlanActivityQueryContext : IQueryActorContext<StrategyTradePlanActivityQueryActor>
{
    IDbContextFactory DbFactory { get; }

    ILogger<StrategyTradePlanActivityQueryActor> Logger { get; }
}
