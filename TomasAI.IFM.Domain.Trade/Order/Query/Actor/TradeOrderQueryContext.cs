using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Order.Query.Actor;
public sealed class TradeOrderQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<TradeOrderQueryActor> logger) : QueryActorContext(supervisor, new ActorMailboxId(ActorType.Query, TradeOrderQueryActor.ActorName)), ITradeOrderQueryContext
{
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public ILogger<TradeOrderQueryActor> Logger { get; } = logger;
}
