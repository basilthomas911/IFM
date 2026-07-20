using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.CommandParameters;

/// <summary>
/// Represents the parameters required to generate a futures TDI signal.
/// </summary>
/// <param name="FuturesTdiSignalId">Identifier describing the target futures contract and value date for TDI signal generation.</param>
/// <param name="FuturesRsiSignals">Collection of RSI signals used as input factors for computing the TDI signal.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record GenerateFuturesTdiSignalParameter(
    FuturesTdiSignalId FuturesTdiSignalId,
    FuturesRsiSignalReadModel[] FuturesRsiSignals,
    int ErrorCode)
    : ICommandParameter;
