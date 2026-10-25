using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Query.Actor;
public sealed class VerticalSpreadTradePlanQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<VerticalSpreadTradePlanQueryActor> logger) : QueryActorContext(supervisor, new(ActorType.Query, VerticalSpreadTradePlanQueryActor.ActorName)), IVerticalSpreadTradePlanQueryContext
{
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public ILogger<VerticalSpreadTradePlanQueryActor> Logger { get; } = logger;
}
