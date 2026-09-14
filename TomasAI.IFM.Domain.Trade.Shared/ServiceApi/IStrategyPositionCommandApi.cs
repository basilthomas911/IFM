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
}
