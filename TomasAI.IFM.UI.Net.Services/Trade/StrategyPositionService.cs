using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.Net.Services.Trade;

/// <summary>Provides UI-safe access to current strategy-position state and explicit lifecycle commands.</summary>
/// <param name="commands">The strategy-position command boundary.</param>
/// <param name="queries">The strategy-position query boundary.</param>
public sealed class StrategyPositionService(
    IStrategyPositionCommandApi commands,
    IStrategyPositionQueryApi queries) : UiServiceBase<StrategyPositionService>
{
    readonly IStrategyPositionCommandApi _commands =
        commands ?? throw new ArgumentNullException(nameof(commands));
    readonly IStrategyPositionQueryApi _queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    /// <summary>Loads the current snapshot for a strategy-position stream.</summary>
    /// <param name="positionId">The complete position identity.</param>
    /// <param name="strategyKind">The strategy that owns the position.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The current position snapshot.</returns>
    public async Task<StrategyPositionSnapshot> GetCurrentAsync(
        StrategyPositionId positionId,
        TradeStrategyKind strategyKind,
        CancellationToken cancellationToken = default)
    {
        var result = await _queries.GetCurrentAsync(positionId, strategyKind, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success || result.Value is null)
            throw new InvalidOperationException(
                $"Strategy position query failed ({result.ErrorCode}): {result.ErrorMessage ?? "The strategy position could not be loaded."}");
        return result.Value;
    }

    /// <summary>Moves an open strategy position to its end-of-day state and returns its command identifier.</summary>
    /// <param name="positionId">The position stream to update.</param>
    /// <param name="strategyKind">The strategy that owns the position.</param>
    /// <param name="effectiveAtUtc">The UTC effective time.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>The accepted command identifier.</returns>
    public async Task<Guid> EndOfDayAsync(
        StrategyPositionId positionId,
        TradeStrategyKind strategyKind,
        DateTime effectiveAtUtc,
        CancellationToken cancellationToken = default)
    {
        var result = await _commands.EndOfDayAsync(
            positionId, strategyKind, effectiveAtUtc, cancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Value == Guid.Empty)
            throw new InvalidOperationException(
                $"End-of-day position processing failed ({result.ErrorCode}): {result.ErrorMessage}");
        return result.Value;
    }
}
