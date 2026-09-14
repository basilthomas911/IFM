using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

public interface ITradePlanDbReadContext
{
    Task<StrategyTradePlanSnapshot?> GetCurrentAsync(StrategyPositionId positionId, TradeStrategyKind strategy,
        DateOnly valueDate, CancellationToken cancellationToken = default);
    Task<QueryPage<StrategyTradePlanSnapshot>> GetHistoryAsync(StrategyPositionId positionId,
        TradeStrategyKind strategy, DateOnly valueDate, int pageSize, byte[]? pagingState = null,
        CancellationToken cancellationToken = default);
    Task<QueryPage<StrategyTradePlanSnapshot>> GetActivityAsync(DateOnly valueDate, int pageSize,
        byte[]? pagingState = null, CancellationToken cancellationToken = default);
    Task<ExitPositionWorkflowProjection?> GetCurrentExitWorkflowAsync(StrategyPositionId positionId,
        DateOnly valueDate, CancellationToken cancellationToken = default);
    Task<QueryPage<ExitPositionWorkflowProjection>> GetExitWorkflowTimelineAsync(
        StrategyPositionId positionId, DateOnly valueDate, int pageSize, byte[]? pagingState = null,
        CancellationToken cancellationToken = default);
}

public interface ITradePlanDbWriteContext
{
    Task ProjectMaterialAsync(StrategyTradePlanSnapshot plan, CancellationToken cancellationToken = default);
    Task ProjectExitWorkflowAsync(ExitPositionWorkflowProjection workflow,
        CancellationToken cancellationToken = default);
}

public interface ITradePlanDbContext : IObjectRepository<TradePlanDbContext>,
    ITradePlanDbReadContext, ITradePlanDbWriteContext
{
    ITradePlanDbReadContext DbReader { get; }
    ITradePlanDbWriteContext DbWriter { get; }
}
