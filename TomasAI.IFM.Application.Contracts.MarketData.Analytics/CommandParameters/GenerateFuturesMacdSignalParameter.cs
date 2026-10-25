using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to generate a futures MACD signal.
/// </summary>
/// <param name="FuturesMacdSignalId">Identifier describing the target futures contract and value date for MACD signal generation.</param>
/// <param name="FuturesPrice">The price of the futures contract.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record GenerateFuturesMacdSignalParameter(
    FuturesMacdSignalId FuturesMacdSignalId,
    decimal FuturesPrice,
    int ErrorCode)
    : ICommandParameter;
