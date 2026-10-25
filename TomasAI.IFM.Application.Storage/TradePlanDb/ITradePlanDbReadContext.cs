using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

/// <summary>
/// Defines Trade Plan database queries.
/// </summary>
public interface ITradePlanDbReadContext
{
    Task<StrategyTradePlanChartSeries> GetChartRangeAsync(StrategyPositionId positionId, DateOnly firstValueDate, DateOnly lastValueDate, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default);

    /// <summary>Reads only the requested offset window, anchored to a revision for stable scrolling.</summary>
    Task<StrategyTradePlanHistoryPage> GetOffsetHistoryAsync(StrategyPositionId positionId, TradeStrategyKind strategy, DateOnly valueDate, int offset, int pageSize, long? maximumRevision = null, CancellationToken cancellationToken = default);

    /// <summary>Gets the current material Trade Plan for a strategy position.</summary>
    /// <param name="positionId">The strategy-position identity.</param>
    /// <param name="strategy">The trade strategy that selects the materialized-plan table.</param>
    /// <param name="valueDate">The plan value date.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>The current plan, or <see langword="null"/> when no plan is stored.</returns>
    Task<StrategyTradePlanSnapshot?> GetCurrentAsync(
        StrategyPositionId positionId,
        TradeStrategyKind strategy,
        DateOnly valueDate,
        CancellationToken cancellationToken = default);

    /// <summary>Gets one page of material Trade Plan history for a strategy position.</summary>
    /// <param name="positionId">The strategy-position identity.</param>
    /// <param name="strategy">The trade strategy that selects the materialized-plan table.</param>
    /// <param name="valueDate">The plan value date.</param>
    /// <param name="pageSize">The maximum number of plans to return.</param>
    /// <param name="pagingState">The provider paging state from the preceding page.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>A page of plans ordered by descending revision.</returns>
    Task<QueryPage<StrategyTradePlanSnapshot>> GetHistoryAsync(
        StrategyPositionId positionId,
        TradeStrategyKind strategy,
        DateOnly valueDate,
        int pageSize,
        byte[]? pagingState = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets one page of Trade Plan activity for a value date.</summary>
    /// <param name="valueDate">The activity value date.</param>
    /// <param name="pageSize">The maximum number of plans to return.</param>
    /// <param name="pagingState">The provider paging state from the preceding page.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>A page of material-plan activity.</returns>
    Task<QueryPage<StrategyTradePlanSnapshot>> GetActivityAsync(
        DateOnly valueDate,
        int pageSize,
        byte[]? pagingState = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the current exit-position workflow for a strategy position.</summary>
    /// <param name="positionId">The strategy-position identity.</param>
    /// <param name="valueDate">The workflow value date.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>The current workflow projection, or <see langword="null"/> when none is stored.</returns>
    Task<ExitPositionWorkflowProjection?> GetCurrentExitWorkflowAsync(
        StrategyPositionId positionId,
        DateOnly valueDate,
        CancellationToken cancellationToken = default);

    /// <summary>Gets one page of the exit-position workflow timeline.</summary>
    /// <param name="positionId">The strategy-position identity.</param>
    /// <param name="valueDate">The workflow value date.</param>
    /// <param name="pageSize">The maximum number of workflow projections to return.</param>
    /// <param name="pagingState">The provider paging state from the preceding page.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>A page of workflow projections ordered by descending update time.</returns>
    Task<QueryPage<ExitPositionWorkflowProjection>> GetExitWorkflowTimelineAsync(
        StrategyPositionId positionId,
        DateOnly valueDate,
        int pageSize,
        byte[]? pagingState = null,
        CancellationToken cancellationToken = default);
    /// <summary>Reads all recorded exit stages under a shared table lock for an explicitly quiesced baseline capture.</summary>
    /// <param name="transaction">The transaction retaining the table lock until its owner completes.</param>
    /// <param name="cancellationToken">Cancels the ordered query.</param>
    /// <returns>All persisted stage snapshots in deterministic identity and revision order.</returns>
    Task<IReadOnlyList<ExitPositionWorkflowProjection>> ReadExitWorkflowBaselineAsync(IEnlistedPostgresTransaction transaction, CancellationToken cancellationToken = default);
}
