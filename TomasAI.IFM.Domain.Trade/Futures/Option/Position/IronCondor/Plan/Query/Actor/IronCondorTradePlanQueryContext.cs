using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query.Actor;
public sealed class IronCondorTradePlanQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<IronCondorTradePlanQueryActor> logger, TimeProvider? clock = null) : QueryActorContext(supervisor, new(ActorType.Query, IronCondorTradePlanQueryActor.ActorName)), IIronCondorTradePlanQueryContext
{
    public TimeProvider Clock { get; } = clock ?? TimeProvider.System;
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public ILogger<IronCondorTradePlanQueryActor> Logger { get; } = logger;
}
