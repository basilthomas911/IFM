using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.CommandParameters;

/// <summary>
/// Represents the parameters required to generate a futures ITI signal.
/// </summary>
/// <param name="ContractId">The futures contract identifier.</param>
/// <param name="ValueDate">The value date for which the signal is generated.</param>
/// <param name="Timestamp">The timestamp of the source data used to generate the signal.</param>
/// <param name="FuturesPrice">The latest futures price.</param>
/// <param name="VixFuturesPrice">The latest VIX futures price.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record GenerateFuturesItiSignalParameter(
    string ContractId,
    DateOnly ValueDate,
    TradeTimePeriodType TimePeriod,
    DateTime Timestamp,
    double FuturesPrice,
    double VixFuturesPrice,
    int ErrorCode)
    : ICommandParameter;
