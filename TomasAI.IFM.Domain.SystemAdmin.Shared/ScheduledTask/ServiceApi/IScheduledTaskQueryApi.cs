using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi;
/// <summary>Exposes persisted administrative read models and portable cron preview.</summary>
public interface IScheduledTaskQueryApi
{
    /// <summary>Reads retained stdout through the persisted run identity.</summary>
    ValueTask<ServiceResult<ScheduledTaskOutputPage>> GetScheduledTaskOutputAsync(GetScheduledTaskOutputQuery query, CancellationToken cancellationToken = default);
    /// <summary>Reads a resumable page of all retained occurrences.</summary>
    ValueTask<ServiceResult<ScheduledTaskRunPage>> GetScheduledTaskRunHistoryAsync(GetScheduledTaskRunHistoryQuery query, CancellationToken cancellationToken = default);
    /// <summary>Reads GetScheduledTaskCatalog using its typed query route.</summary>
    ValueTask<ServiceResult<ScheduledTaskCatalog>> GetScheduledTaskCatalogAsync(GetScheduledTaskCatalogQuery query, CancellationToken cancellationToken = default);
    /// <summary>Reads GetScheduledTasksDashboard using its typed query route.</summary>
    ValueTask<ServiceResult<ScheduledTasksDashboard>> GetScheduledTasksDashboardAsync(GetScheduledTasksDashboardQuery query, CancellationToken cancellationToken = default);
    /// <summary>Reads GetScheduledTask using its typed query route.</summary>
    ValueTask<ServiceResult<ScheduledTaskDefinition>> GetScheduledTaskAsync(GetScheduledTaskQuery query, CancellationToken cancellationToken = default);
    /// <summary>Reads PreviewScheduledTaskSchedule using its typed query route.</summary>
    ValueTask<ServiceResult<ScheduledTaskSchedulePreview>> PreviewScheduledTaskScheduleAsync(PreviewScheduledTaskScheduleQuery query, CancellationToken cancellationToken = default);
    /// <summary>Reads GetScheduledTaskRun using its typed query route.</summary>
    ValueTask<ServiceResult<ScheduledTaskRun>> GetScheduledTaskRunAsync(GetScheduledTaskRunQuery query, CancellationToken cancellationToken = default);
    /// <summary>Reads ListScheduledTaskRuns using its typed query route.</summary>
    ValueTask<ServiceResult<ScheduledTaskRun[]>> ListScheduledTaskRunsAsync(ListScheduledTaskRunsQuery query, CancellationToken cancellationToken = default);
    /// <summary>Reads GetScheduledTaskHostHealth using its typed query route.</summary>
    ValueTask<ServiceResult<ScheduledTaskHostCapability>> GetScheduledTaskHostHealthAsync(GetScheduledTaskHostHealthQuery query, CancellationToken cancellationToken = default);
    /// <summary>Reads a bounded page of persisted open strategy positions.</summary>
    ValueTask<ServiceResult<ScheduledMarketPositionPage>> GetScheduledMarketPositionsAsync(GetScheduledMarketPositionsQuery query, CancellationToken cancellationToken = default);
}
