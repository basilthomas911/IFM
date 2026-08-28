using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ServiceApi;

/// <summary>Defines the public NATS query API for Intrinsic Time Strategy workflow read models.</summary>
public interface IIntrinsicTimeStrategyWorkflowQueryApi
{
    /// <summary>Gets one workflow by identity.</summary>
    Task<ServiceResult<IntrinsicTimeStrategyWorkflowReadModel>> GetByIdAsync(StrategyWorkflowId workflowId, long minimumRevision = 0);
    /// <summary>Gets the active workflow for an entity.</summary>
    Task<ServiceResult<ActiveIntrinsicTimeStrategyWorkflowReadModel>> GetActiveAsync(string workflowEntityId, long minimumRevision = 0);
    /// <summary>Gets start attempts for an entity.</summary>
    Task<ServiceResult<IntrinsicTimeStrategyWorkflowStartAttemptReadModel[]>> GetStartAttemptsAsync(string workflowEntityId, DateTime beforeUtc, int pageSize);
    /// <summary>Gets one stage state.</summary>
    Task<ServiceResult<StrategyWorkflowStageState>> GetStageStateAsync(StrategyWorkflowId workflowId, StrategyWorkflowStage stage, long minimumRevision = 0);
    /// <summary>Gets a timeline page.</summary>
    Task<ServiceResult<IntrinsicTimeStrategyWorkflowTimelineReadModel[]>> GetTimelineAsync(StrategyWorkflowId workflowId, long afterEventId, int pageSize);
    /// <summary>Gets recent workflows for an entity.</summary>
    Task<ServiceResult<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>> GetRecentAsync(string workflowEntityId, DateTime beforeUtc, int pageSize);
    /// <summary>Gets completed workflows in a date range.</summary>
    Task<ServiceResult<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>> GetCompletedAsync(DateOnly startDate, DateOnly endDate, int pageSize);
    /// <summary>Gets stopped workflows in a date range.</summary>
    Task<ServiceResult<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>> GetStoppedAsync(DateOnly startDate, DateOnly endDate, int pageSize);
    /// <summary>Gets the read-only operational condition for one stable workflow entity.</summary>
    Task<ServiceResult<IntrinsicTimeStrategyWorkflowObservationReadModel>> GetObservationAsync(
        IntrinsicTimeStrategyWorkflowEntityId workflowEntity);
}
