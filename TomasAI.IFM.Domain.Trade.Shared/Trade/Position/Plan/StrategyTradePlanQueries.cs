using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

/// <summary>A bounded page of material strategy Trade Plan snapshots.</summary>
[MessagePackObject]
public sealed record StrategyTradePlanHistoryPage(
    [property: Key(0)] StrategyTradePlanSnapshot[] Items,
    [property: Key(1)] byte[]? PagingState);

/// <summary>A bounded page of material Trade Plans across strategies for one value date.</summary>
[MessagePackObject]
public sealed record StrategyTradePlanActivityPage(
    [property: Key(0)] StrategyTradePlanSnapshot[] Items,
    [property: Key(1)] byte[]? PagingState);

/// <summary>Client read surface for current and historical strategy Trade Plans.</summary>
public interface IStrategyTradePlanQueryApi
{
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
