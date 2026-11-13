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
    /// <summary>Reads the final forward observation at or before each sealed value date's EOD boundary.</summary>
    /// <param name="positionId">The position id supplied by the caller.</param>
    /// <param name="dayEndsUtc">The day ends utc supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<IReadOnlyDictionary<DateOnly, decimal?>> GetDailyForwardSpreadsAsync(StrategyPositionId positionId,
        IReadOnlyDictionary<DateOnly, DateTime> dayEndsUtc, CancellationToken cancellationToken = default);

    /// <summary>Reads chart range from the committed projection.</summary>
    /// <param name="positionId">The position id supplied by the caller.</param>
    /// <param name="firstValueDate">The first value date supplied by the caller.</param>
    /// <param name="lastValueDate">The last value date supplied by the caller.</param>
    /// <param name="startUtc">The start utc supplied by the caller.</param>
    /// <param name="endUtc">The end utc supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<StrategyTradePlanChartSeries> GetChartRangeAsync(StrategyPositionId positionId, DateOnly firstValueDate, DateOnly lastValueDate, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default);

    /// <summary>Reads only the requested offset window, anchored to a revision for stable scrolling.</summary>
    /// <param name="positionId">The position id supplied by the caller.</param>
    /// <param name="strategy">The strategy supplied by the caller.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <param name="offset">The zero-based offset within the fixed revision selection.</param>
    /// <param name="pageSize">The maximum size of the requested page.</param>
    /// <param name="maximumRevision">The inclusive upper revision bound for the read.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<ITradePlanOffsetReadSession> OpenOffsetHistoryAsync(StrategyPositionId positionId, TradeStrategyKind strategy, DateOnly valueDate, int offset, int pageSize, long? maximumRevision = null, CancellationToken cancellationToken = default);

    /// <summary>Opens anchored history, seeking from the preceding page when a continuation is supplied.</summary>
    Task<ITradePlanOffsetReadSession> OpenHistoryPageAsync(StrategyPositionId positionId, TradeStrategyKind strategy, DateOnly valueDate,
        int offset, int pageSize, long? maximumRevision, byte[]? pagingState, CancellationToken cancellationToken = default);

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

/// <summary>Owns a repeatable PostgreSQL read snapshot until the caller disposes the session.</summary>
public interface ITradePlanOffsetReadSession : IAsyncDisposable
{
    /// <summary>The fixed history page, selected revision and chart end from this snapshot.</summary>
    StrategyTradePlanHistoryPage Page { get; }

    /// <summary>Reads price-only samples at this session's fixed revision.</summary>
    /// <param name="startUtc">The inclusive start of the chart.</param>
    /// <param name="endUtc">The inclusive end of the chart.</param>
    /// <param name="token">Cancels the read.</param>
    /// <returns>The selected minute samples in time order.</returns>
    Task<StrategyTradePlanChartPoint[]> ReadChartAsync(DateTime startUtc, DateTime endUtc, CancellationToken token);
}
