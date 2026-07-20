using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.CommandParameters;

/// <summary>
/// Represents the parameters required to start a futures RSI signal.
/// </summary>
/// <param name="EntityId">The RSI signal entity identifier (contract + value date).</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record StartFuturesRsiSignalParameter(FuturesRsiSignalEntityId EntityId, int ErrorCode)
    : ICommandParameter;
