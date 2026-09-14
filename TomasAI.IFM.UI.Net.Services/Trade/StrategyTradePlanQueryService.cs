using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

namespace TomasAI.IFM.UI.Net.Services.Trade;

/// <summary>Provides UI-safe access to material strategy Trade Plans and exit-workflow history.</summary>
/// <param name="queryApi">The strategy Trade Plan query boundary.</param>
public sealed class StrategyTradePlanQueryService(IStrategyTradePlanQueryApi queryApi)
    : UiServiceBase<StrategyTradePlanQueryService>
{
    readonly IStrategyTradePlanQueryApi _queryApi =
        queryApi ?? throw new ArgumentNullException(nameof(queryApi));

    /// <summary>Loads the current material plan for a known strategy-position identity.</summary>
    /// <param name="positionId">The complete position identity.</param>
    /// <param name="strategyKind">The strategy that owns the plan.</param>
    /// <param name="valueDate">The plan value date.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The current material plan, or <see langword="null"/> when none has been projected.</returns>
    public async Task<StrategyTradePlanSnapshot?> GetCurrentAsync(
        StrategyPositionId positionId,
        TradeStrategyKind strategyKind,
        DateOnly valueDate,
        CancellationToken cancellationToken = default)
    {
        var result = strategyKind switch
        {
            TradeStrategyKind.IronCondor => await _queryApi.GetCurrentIronCondorAsync(
                positionId, valueDate, cancellationToken).ConfigureAwait(false),
            TradeStrategyKind.VerticalSpread => await _queryApi.GetCurrentVerticalSpreadAsync(
                positionId, valueDate, cancellationToken).ConfigureAwait(false),
            TradeStrategyKind.FuturesOutright => await _queryApi.GetCurrentFuturesAsync(
                positionId, valueDate, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(strategyKind), strategyKind,
                "Unsupported Trade Plan strategy.")
        };
        if (!result.Success)
            throw new InvalidOperationException(
                $"Strategy Trade Plan query failed ({result.ErrorCode}): {result.ErrorMessage}");
        return result.Value;
    }

    /// <summary>Loads one bounded page of material plan activity for a value date.</summary>
    /// <param name="valueDate">The value date to search.</param>
    /// <param name="pageSize">The maximum number of activity rows to return.</param>
    /// <param name="pagingState">The continuation state returned by the previous page.</param>
    /// <param name="onCompleted">Receives the loaded activity page.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>A task that completes after the callback receives the result.</returns>
    public Task GetActivityAsync(DateOnly valueDate, int pageSize, byte[]? pagingState,
        Action<StrategyTradePlanActivityPage> onCompleted,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => _queryApi.GetActivityAsync(valueDate, pageSize, pagingState,
            cancellationToken), onCompleted);

    /// <summary>Loads the current material plan for the supplied strategy position.</summary>
    /// <param name="selected">The selected strategy position and value date.</param>
    /// <param name="onCompleted">Receives the current material plan, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>A task that completes after the callback receives the result.</returns>
    public Task GetCurrentAsync(StrategyTradePlanSnapshot selected,
        Action<StrategyTradePlanSnapshot?> onCompleted,
        CancellationToken cancellationToken = default) => selected.Position.StrategyKind switch
    {
        TradeStrategyKind.IronCondor => ExecuteAsync(() =>
            _queryApi.GetCurrentIronCondorAsync(selected.Position.Id, selected.ValueDate,
                cancellationToken), onCompleted),
        TradeStrategyKind.VerticalSpread => ExecuteAsync(() =>
            _queryApi.GetCurrentVerticalSpreadAsync(selected.Position.Id, selected.ValueDate,
                cancellationToken), onCompleted),
        TradeStrategyKind.FuturesOutright => ExecuteAsync(() =>
            _queryApi.GetCurrentFuturesAsync(selected.Position.Id, selected.ValueDate,
                cancellationToken), onCompleted),
        _ => throw new ArgumentOutOfRangeException(nameof(selected),
            selected.Position.StrategyKind, "Unsupported Trade Plan strategy.")
    };

    /// <summary>Loads one bounded page of material plan history for the supplied strategy position.</summary>
    /// <param name="selected">The selected strategy position and value date.</param>
    /// <param name="pageSize">The maximum number of history rows to return.</param>
    /// <param name="pagingState">The continuation state returned by the previous page.</param>
    /// <param name="onCompleted">Receives the loaded history page.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>A task that completes after the callback receives the result.</returns>
    public Task GetHistoryAsync(StrategyTradePlanSnapshot selected, int pageSize,
        byte[]? pagingState, Action<StrategyTradePlanHistoryPage> onCompleted,
        CancellationToken cancellationToken = default) => selected.Position.StrategyKind switch
    {
        TradeStrategyKind.IronCondor => ExecuteAsync(() =>
            _queryApi.GetIronCondorHistoryAsync(selected.Position.Id, selected.ValueDate,
                pageSize, pagingState, cancellationToken), onCompleted),
        TradeStrategyKind.VerticalSpread => ExecuteAsync(() =>
            _queryApi.GetVerticalSpreadHistoryAsync(selected.Position.Id, selected.ValueDate,
                pageSize, pagingState, cancellationToken), onCompleted),
        TradeStrategyKind.FuturesOutright => ExecuteAsync(() =>
            _queryApi.GetFuturesHistoryAsync(selected.Position.Id, selected.ValueDate,
                pageSize, pagingState, cancellationToken), onCompleted),
        _ => throw new ArgumentOutOfRangeException(nameof(selected),
            selected.Position.StrategyKind, "Unsupported Trade Plan strategy.")
    };

    /// <summary>Loads the current exit-workflow stage for a strategy position.</summary>
    /// <param name="selected">The selected strategy position and value date.</param>
    /// <param name="onCompleted">Receives the current exit-workflow projection, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>A task that completes after the callback receives the result.</returns>
    public Task GetCurrentExitWorkflowAsync(StrategyTradePlanSnapshot selected,
        Action<ExitPositionWorkflowProjection?> onCompleted,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => _queryApi.GetCurrentExitWorkflowAsync(selected.Position.Id,
            selected.ValueDate, cancellationToken), onCompleted);

    /// <summary>Loads one bounded page of exit-workflow history for a strategy position.</summary>
    /// <param name="selected">The selected strategy position and value date.</param>
    /// <param name="pageSize">The maximum number of workflow rows to return.</param>
    /// <param name="pagingState">The continuation state returned by the previous page.</param>
    /// <param name="onCompleted">Receives the loaded workflow timeline.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>A task that completes after the callback receives the result.</returns>
    public Task GetExitWorkflowTimelineAsync(StrategyTradePlanSnapshot selected, int pageSize,
        byte[]? pagingState, Action<PositionExitWorkflowHistoryPage> onCompleted,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => _queryApi.GetExitWorkflowTimelineAsync(selected.Position.Id,
            selected.ValueDate, pageSize, pagingState, cancellationToken), onCompleted);
}
