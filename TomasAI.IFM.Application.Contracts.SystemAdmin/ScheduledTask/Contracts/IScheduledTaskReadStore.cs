using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
/// <summary>Reads only persisted PostgreSQL scheduled-task projections.</summary>
public interface IScheduledTaskReadStore
{
    /// <summary>Reads the latest session with positions finalized, including incomplete close attempts.</summary>
    /// <param name="environment">The validated environment identity.</param>
    /// <param name="cancellationToken">Cancels the persistence operation.</param>
    /// <returns>The persisted result, or null when no matching row exists.</returns>
    ValueTask<DateOnly?> GetCompletedEndOfDayAsync(string environment, CancellationToken cancellationToken = default);
    /// <summary>Reads one persisted definition by its owning host and identity.</summary>
    /// <param name="environment">The validated environment identity.</param>
    /// <param name="hostId">The validated owning host identity.</param>
    /// <param name="id">The accepted schedule identity.</param>
    /// <param name="cancellationToken">Cancels the persistence operation.</param>
    /// <returns>The persisted result, or null when no matching row exists.</returns>
    ValueTask<ScheduledTaskDefinition?> GetDefinitionAsync(string environment, string hostId, ScheduledTaskId id, CancellationToken cancellationToken = default);
    /// <summary>Reads a bounded page of definitions for a host.</summary>
    /// <param name="environment">The validated environment identity.</param>
    /// <param name="hostId">The validated owning host identity.</param>
    /// <param name="limit">The requested bounded row count.</param>
    /// <param name="cancellationToken">Cancels the persistence operation.</param>
    /// <returns>The persisted rows in the requested scope.</returns>
    ValueTask<ScheduledTaskDefinition[]> GetDefinitionsAsync(string environment, string hostId, int limit, CancellationToken cancellationToken = default);
    /// <summary>Reads persisted deployment and capability facts.</summary>
    /// <param name="environment">The validated environment identity.</param>
    /// <param name="hostId">The validated owning host identity.</param>
    /// <param name="cancellationToken">Cancels the persistence operation.</param>
    /// <returns>The persisted result, or null when no matching row exists.</returns>
    ValueTask<ScheduledTaskCatalog?> GetCatalogAsync(string environment, string hostId, CancellationToken cancellationToken = default);
    /// <summary>Reads one occurrence receipt from the persisted run table.</summary>
    /// <param name="environment">The validated environment identity.</param>
    /// <param name="hostId">The validated owning host identity.</param>
    /// <param name="scheduleId">The accepted schedule identity.</param>
    /// <param name="runId">The accepted occurrence identity.</param>
    /// <param name="intendedFireTimeUtc">The UTC occurrence boundary.</param>
    /// <param name="cancellationToken">Cancels the persistence operation.</param>
    /// <returns>The persisted result, or null when no matching row exists.</returns>
    ValueTask<ScheduledTaskRun?> GetRunAsync(string environment, string hostId, ScheduledTaskId scheduleId, ScheduledTaskId runId, DateTimeOffset intendedFireTimeUtc, CancellationToken cancellationToken = default);
    /// <summary>Reads an opaque-cursor page of retained occurrences for one schedule.</summary>
    /// <param name="selection">The filters and continuation already validated by the domain query handler.</param>
    /// <param name="cancellationToken">Cancels the persistence operation.</param>
    /// <returns>The persisted rows in the requested scope.</returns>
    ValueTask<ScheduledTaskRunPage> GetRunHistoryAsync(ScheduledTaskHistorySelection selection, CancellationToken cancellationToken = default);
    /// <summary>Reads a bounded page of recent occurrences for one schedule.</summary>
    /// <param name="environment">The validated environment identity.</param>
    /// <param name="hostId">The validated owning host identity.</param>
    /// <param name="scheduleId">The accepted schedule identity.</param>
    /// <param name="limit">The requested bounded row count.</param>
    /// <param name="cancellationToken">Cancels the persistence operation.</param>
    /// <returns>The persisted rows in the requested scope.</returns>
    ValueTask<ScheduledTaskRun[]> GetRunsAsync(string environment, string hostId, ScheduledTaskId scheduleId, int limit, CancellationToken cancellationToken = default);
}
