using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.CommandParameters;

/// <summary>
/// Represents the parameters required to clear a futures ITI signal hold trade.
/// </summary>
/// <param name="ItiSignalId">The ITI signal identifier.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record ClearFuturesItiSignalHoldTradeParameter(FuturesItiSignalId ItiSignalId, int ErrorCode)
    : ICommandParameter;
