using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

/// <summary>Provides current strategy-position snapshots without exposing storage details to callers.</summary>
public interface IStrategyPositionQueryApi
{
    /// <summary>Reads the current state of one strategy position.</summary>
    /// <param name="positionId">The complete position identity.</param>
    /// <param name="strategyKind">The strategy that owns the position.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The current position snapshot.</returns>
    Task<ServiceResult<StrategyPositionSnapshot>> GetCurrentAsync(
        StrategyPositionId positionId,
        TradeStrategyKind strategyKind,
        CancellationToken cancellationToken = default);
    /// <summary>Reads every persisted position-history row through the strategy query actor.</summary>
    /// <param name="positionId">The complete position identity.</param>
    /// <param name="strategyKind">The strategy that owns the position.</param>
    /// <param name="fromUtc">The inclusive UTC start time.</param>
    /// <param name="toUtc">The inclusive UTC end time.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>All persisted history rows for this position.</returns>
    Task<ServiceResult<StrategyPositionSnapshot[]>> GetHistoryAsync(
        StrategyPositionId positionId, TradeStrategyKind strategyKind,
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}
