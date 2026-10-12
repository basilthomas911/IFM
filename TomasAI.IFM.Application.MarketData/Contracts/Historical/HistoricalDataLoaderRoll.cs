using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Describes one actual-contract transition in a continuation series.</summary>
public sealed record HistoricalDataLoaderRoll(
    string SeriesKey,
    DateOnly ValueDate,
    string PreviousContractId,
    string CurrentContractId);
