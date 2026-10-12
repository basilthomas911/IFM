using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Resolves domain series requests to exact provider request profiles.</summary>
public interface IHistoricalSeriesRequestResolver
{
    /// <summary>Resolves one domain series to an exact provider request.</summary>
    HistoricalProviderRequest Resolve(
        MarketDataHistoricalRequest request,
        MarketDataHistoricalSeriesRequest series);

    /// <summary>Resolves an actual domain contract from a provider record.</summary>
    string ResolveContractId(MarketDataHistoricalSeriesRequest series, HistoricalProviderRecord record);
}
