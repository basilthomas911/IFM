using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Order.Query.Actor;
public sealed class TradeOrderQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<TradeOrderQueryActor> logger, TimeProvider? preparationClock = null, Microsoft.Extensions.Configuration.IConfiguration? preparationConfiguration = null, TomasAI.IFM.Domain.MarketData.Shared.IFuturesMarketSessionAuthority? marketSessionAuthority = null) : QueryActorContext(supervisor, new ActorMailboxId(ActorType.Query, TradeOrderQueryActor.ActorName)), ITradeOrderQueryContext
{
    public TimeProvider PreparationClock { get; } = preparationClock ?? TimeProvider.System;
    public Microsoft.Extensions.Configuration.IConfiguration? PreparationConfiguration { get; } = preparationConfiguration;
    public TomasAI.IFM.Domain.MarketData.Shared.IFuturesMarketSessionAuthority? MarketSessionAuthority { get; } = marketSessionAuthority;
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public ILogger<TradeOrderQueryActor> Logger { get; } = logger;
}
