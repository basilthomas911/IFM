using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to set a futures ITI signal hold trade.
/// </summary>
/// <param name="ItiSignalId">The ITI signal identifier.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record SetFuturesItiSignalHoldTradeParameter(FuturesItiSignalId ItiSignalId, int ErrorCode)
    : ICommandParameter;
