using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.CommandParameters;

/// <summary>
/// Represents the parameters required to generate a futures ATR signal.
/// </summary>
/// <param name="FuturesAtrSignalId">Identifier describing the target futures contract and value date for ATR signal generation.</param>
/// <param name="FuturesItiSignals">Collection of ITI signals used as input factors for computing the ATR signal.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record GenerateFuturesAtrSignalParameter(
    FuturesAtrSignalId FuturesAtrSignalId,
    FuturesItiSignalV2ReadModel[] FuturesItiSignals,
    int ErrorCode)
    : ICommandParameter;
