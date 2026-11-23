using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

/// <summary>Provides typed commands for the strategy-position actors that own live trade state.</summary>
public interface IStrategyPositionCommandApi
{
    /// <summary>Moves the selected open strategy position to its end-of-day state.</summary>
    /// <param name="positionId">The position stream to update.</param>
    /// <param name="strategyKind">The strategy actor that owns the stream.</param>
    /// <param name="effectiveAtUtc">The UTC end-of-day effective time.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The command result and its correlation identifier.</returns>
    Task<ServiceResult<Guid>> EndOfDayAsync(
        StrategyPositionId positionId,
        TradeStrategyKind strategyKind,
        DateTime effectiveAtUtc,
        CancellationToken cancellationToken = default);
    /// <summary>Seals the explicit ended exchange date after feeds are confirmed stopped.</summary>
    Task<ServiceResult<Guid>> EndOfDayAsync(StrategyPositionId positionId, TradeStrategyKind strategyKind,
        DateOnly valueDate, DateTime closeBoundaryUtc, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> RepairEndOfDayWithBaselineAsync(StrategyPositionSnapshot source, long sourceEventId,
        DateOnly valueDate, DateTime closeBoundaryUtc, StoredPositionClosingQuote[] quotes, StrategyPositionSnapshot? previousEndOfDay,
        CancellationToken cancellationToken = default) => RepairEndOfDayAsync(source,sourceEventId,valueDate,closeBoundaryUtc,quotes,cancellationToken);
    /// <summary>Repairs a completed session using immutable source-event and closing-quote evidence.</summary>
    Task<ServiceResult<Guid>> RepairEndOfDayAsync(StrategyPositionSnapshot source, long sourceEventId,
        DateOnly valueDate, DateTime closeBoundaryUtc, StoredPositionClosingQuote[] quotes,
        CancellationToken cancellationToken = default);
}
