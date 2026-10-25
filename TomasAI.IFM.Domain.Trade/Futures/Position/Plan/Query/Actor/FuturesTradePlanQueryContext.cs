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
public sealed class FuturesTradePlanQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<FuturesTradePlanQueryActor> logger) : QueryActorContext(supervisor, new(ActorType.Query, FuturesTradePlanQueryActor.ActorName)), IFuturesTradePlanQueryContext
{
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public ILogger<FuturesTradePlanQueryActor> Logger { get; } = logger;
}
