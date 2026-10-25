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
public interface ITradeOrderQueryContext : IQueryActorContext<TradeOrderQueryActor>
{
    TimeProvider PreparationClock => TimeProvider.System;
    Microsoft.Extensions.Configuration.IConfiguration? PreparationConfiguration => null;
    TomasAI.IFM.Domain.MarketData.Shared.IFuturesMarketSessionAuthority? MarketSessionAuthority => null;

    IDbContextFactory DbFactory { get; }

    ILogger<TradeOrderQueryActor> Logger { get; }
}
