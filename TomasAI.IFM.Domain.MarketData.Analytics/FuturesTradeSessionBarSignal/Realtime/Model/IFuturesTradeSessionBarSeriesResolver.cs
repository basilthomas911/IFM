using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;

/// <summary>Resolves a routed contract to its explicit contract or continuation-series identity.</summary>
public interface IFuturesTradeSessionBarSeriesResolver
{
    /// <summary>Resolves the configured market series for one live contract.</summary>
    MarketSeriesIdentity Resolve(string contractId);
}
