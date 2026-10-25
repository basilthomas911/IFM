using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

/// <summary>Client read surface for current and historical strategy Trade Plans.</summary>
public interface IStrategyTradePlanQueryApi
{
    Task<ServiceResult<StrategyTradePlanChartSeries>> GetIronCondorChartAsync(StrategyPositionId positionId, DateOnly valueDate, CancellationToken cancellationToken = default);
    /// <summary>Reads only the requested offset window, anchored to a revision for stable scrolling.</summary>
    Task<ServiceResult<StrategyTradePlanHistoryPage>> GetIronCondorHistoryPageAsync(StrategyPositionId positionId, DateOnly valueDate, int offset, int pageSize, long? maximumRevision = null, CancellationToken cancellationToken = default);

    Task<ServiceResult<StrategyTradePlanSnapshot?>> GetCurrentIronCondorAsync(
        StrategyPositionId positionId, DateOnly valueDate, CancellationToken cancellationToken = default);
    Task<ServiceResult<StrategyTradePlanHistoryPage>> GetIronCondorHistoryAsync(
        StrategyPositionId positionId, DateOnly valueDate, int pageSize = 100, byte[]? pagingState = null,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<StrategyTradePlanSnapshot?>> GetCurrentVerticalSpreadAsync(
        StrategyPositionId positionId, DateOnly valueDate, CancellationToken cancellationToken = default);
    Task<ServiceResult<StrategyTradePlanHistoryPage>> GetVerticalSpreadHistoryAsync(
        StrategyPositionId positionId, DateOnly valueDate, int pageSize = 100, byte[]? pagingState = null,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<StrategyTradePlanSnapshot?>> GetCurrentFuturesAsync(
        StrategyPositionId positionId, DateOnly valueDate, CancellationToken cancellationToken = default);
    Task<ServiceResult<StrategyTradePlanHistoryPage>> GetFuturesHistoryAsync(
        StrategyPositionId positionId, DateOnly valueDate, int pageSize = 100, byte[]? pagingState = null,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<StrategyTradePlanActivityPage>> GetActivityAsync(
        DateOnly valueDate, int pageSize = 100, byte[]? pagingState = null,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow.ExitPositionWorkflowProjection?>>
        GetCurrentExitWorkflowAsync(
            StrategyPositionId positionId, DateOnly valueDate,
            CancellationToken cancellationToken = default);
    Task<ServiceResult<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow.PositionExitWorkflowHistoryPage>>
        GetExitWorkflowTimelineAsync(
            StrategyPositionId positionId, DateOnly valueDate, int pageSize = 100,
            byte[]? pagingState = null, CancellationToken cancellationToken = default);
}
