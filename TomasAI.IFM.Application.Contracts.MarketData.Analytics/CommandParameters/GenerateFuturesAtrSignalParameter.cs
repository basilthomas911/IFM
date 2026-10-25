using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to generate a futures ATR signal.
/// </summary>
/// <param name="FuturesAtrSignalId">Identifier describing the target futures contract and value date for ATR signal generation.</param>
/// <param name="Observation">Completed trade-session bar used to advance the Wilder ATR checkpoint.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record GenerateFuturesAtrSignalParameter(
    FuturesAtrSignalId FuturesAtrSignalId,
    FuturesTradeSessionBarReadModel Observation,
    int ErrorCode)
    : ICommandParameter;
