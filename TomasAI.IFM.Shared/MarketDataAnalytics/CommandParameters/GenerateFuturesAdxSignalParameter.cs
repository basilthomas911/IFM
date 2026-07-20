using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.CommandParameters;

/// <summary>
/// Represents the parameters required to generate a futures ADX signal.
/// </summary>
/// <param name="FuturesAdxSignalId">Identifier describing the target futures contract and value date for ADX signal generation.</param>
/// <param name="FuturesItiSignals">Collection of ITI signals used as input factors for computing the ADX signal.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record GenerateFuturesAdxSignalParameter(
    FuturesAdxSignalId FuturesAdxSignalId,
    decimal FuturesPrice,
    int ErrorCode)
    : ICommandParameter;
