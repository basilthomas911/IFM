using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;

/// <summary>Process-local, per-contract dispatch state owned by the realtime event context.</summary>
public sealed class FuturesEodTradeDispatchState
{
    internal readonly FuturesEodTradeReadCache ReadCache = new();
    internal readonly Dictionary<string, FuturesEodTradeWorker> Workers = new(StringComparer.Ordinal);
    internal bool WorkersEnabled;
    internal long Handoffs;
}
