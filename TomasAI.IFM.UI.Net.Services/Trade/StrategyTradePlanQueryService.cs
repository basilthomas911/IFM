using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

namespace TomasAI.IFM.UI.Net.Services.Trade;

/// <summary>Provides UI-safe access to material strategy Trade Plans and exit-workflow history.</summary>
public sealed class StrategyTradePlanQueryService(IStrategyTradePlanQueryApi queryApi)
    : UiServiceBase<StrategyTradePlanQueryService>
{
    readonly IStrategyTradePlanQueryApi _queryApi =
        queryApi ?? throw new ArgumentNullException(nameof(queryApi));

    /// <summary>Loads one bounded page of material plan activity for a value date.</summary>
    public Task GetActivityAsync(DateOnly valueDate, int pageSize, byte[]? pagingState,
        Action<StrategyTradePlanActivityPage> onCompleted,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => _queryApi.GetActivityAsync(valueDate, pageSize, pagingState,
            cancellationToken), onCompleted);

    /// <summary>Loads the current material plan for the supplied strategy position.</summary>
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
    public Task GetCurrentExitWorkflowAsync(StrategyTradePlanSnapshot selected,
        Action<ExitPositionWorkflowProjection?> onCompleted,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => _queryApi.GetCurrentExitWorkflowAsync(selected.Position.Id,
            selected.ValueDate, cancellationToken), onCompleted);

    /// <summary>Loads one bounded page of exit-workflow history for a strategy position.</summary>
    public Task GetExitWorkflowTimelineAsync(StrategyTradePlanSnapshot selected, int pageSize,
        byte[]? pagingState, Action<PositionExitWorkflowHistoryPage> onCompleted,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => _queryApi.GetExitWorkflowTimelineAsync(selected.Position.Id,
            selected.ValueDate, pageSize, pagingState, cancellationToken), onCompleted);
}
