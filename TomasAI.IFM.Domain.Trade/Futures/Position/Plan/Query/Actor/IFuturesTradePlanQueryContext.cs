using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Query.Actor;
public interface IFuturesTradePlanQueryContext : IQueryActorContext<FuturesTradePlanQueryActor>
{
    IDbContextFactory DbFactory { get; }

    ILogger<FuturesTradePlanQueryActor> Logger { get; }
}
