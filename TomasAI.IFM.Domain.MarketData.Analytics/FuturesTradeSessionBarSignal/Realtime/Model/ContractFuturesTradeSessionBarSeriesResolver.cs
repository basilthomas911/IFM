using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;

/// <summary>Uses exact contracts when no continuation mapping is configured.</summary>
public sealed class ContractFuturesTradeSessionBarSeriesResolver : IFuturesTradeSessionBarSeriesResolver
{
    /// <inheritdoc />
    public MarketSeriesIdentity Resolve(string contractId) => MarketSeriesIdentity.ForContract(contractId);
}
