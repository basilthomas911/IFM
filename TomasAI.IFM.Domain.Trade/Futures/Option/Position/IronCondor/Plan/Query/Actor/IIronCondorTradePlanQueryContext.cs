using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query.Actor;
public interface IIronCondorTradePlanQueryContext : IQueryActorContext<IronCondorTradePlanQueryActor>
{
    IDbContextFactory DbFactory { get; }

    ILogger<IronCondorTradePlanQueryActor> Logger { get; }
}
