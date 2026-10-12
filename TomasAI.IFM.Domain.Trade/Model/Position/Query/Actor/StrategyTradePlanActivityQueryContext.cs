using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Query.Actor;
public sealed class StrategyTradePlanActivityQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<StrategyTradePlanActivityQueryActor> logger) : QueryActorContext(supervisor, new(ActorType.Query, StrategyTradePlanActivityQueryActor.ActorName)), IStrategyTradePlanActivityQueryContext
{
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public ILogger<StrategyTradePlanActivityQueryActor> Logger { get; } = logger;
}
