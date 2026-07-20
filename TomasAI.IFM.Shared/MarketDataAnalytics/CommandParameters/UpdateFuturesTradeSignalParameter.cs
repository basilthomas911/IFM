using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.CommandParameters;

/// <summary>
/// Represents the parameters required to update a futures trade signal.
/// </summary>
/// <param name="FuturesEodData">End-of-day futures data used as the core input for the trade signal update.</param>
/// <param name="FuturesRsiSignal">RSI signal metrics used to enrich the trade signal update.</param>
/// <param name="FuturesTdiSignal">TDI signal metrics used to enrich the trade signal update.</param>
/// <param name="FuturesItiSignalData">ITI signal data used to enrich the trade signal update.</param>
/// <param name="VixFuturesPrice">VIX futures price used as an external volatility input.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record UpdateFuturesTradeSignalParameter(
    FuturesEodDataV2ReadModel FuturesEodData,
    FuturesRsiSignalReadModel FuturesRsiSignal,
    FuturesTdiSignalReadModel FuturesTdiSignal,
    FuturesItiSignalDataReadModel FuturesItiSignalData,
    decimal VixFuturesPrice,
    int ErrorCode)
    : ICommandParameter;
