using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.CommandParameters;

/// <summary>
/// Represents the parameters required to stop a futures RSI signal.
/// </summary>
/// <param name="EntityId">The RSI signal entity identifier (contract + value date).</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record StopFuturesRsiSignalParameter(FuturesRsiSignalEntityId EntityId, int ErrorCode)
    : ICommandParameter;
