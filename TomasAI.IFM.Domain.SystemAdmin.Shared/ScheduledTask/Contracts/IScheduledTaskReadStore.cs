namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
/// <summary>Reads only persisted ScyllaDB scheduled-task projections.</summary>
public interface IScheduledTaskReadStore
{
    /// <summary>Reads the latest successfully finalized session for restart-safe operational date rollover.</summary>
    ValueTask<DateOnly?> GetCompletedEndOfDayAsync(string environment, CancellationToken cancellationToken = default);
    /// <summary>Reads one persisted definition by its owning host and identity.</summary>
    ValueTask<ScheduledTaskDefinition?> GetDefinitionAsync(string environment, string hostId, ScheduledTaskId id, CancellationToken cancellationToken = default);
    /// <summary>Reads a bounded page of definitions for a host.</summary>
    ValueTask<ScheduledTaskDefinition[]> GetDefinitionsAsync(string environment, string hostId, int limit, CancellationToken cancellationToken = default);
    /// <summary>Reads persisted deployment and capability facts.</summary>
    ValueTask<ScheduledTaskCatalog?> GetCatalogAsync(string environment, string hostId, CancellationToken cancellationToken = default);
    /// <summary>Reads one occurrence receipt from the persisted run table.</summary>
    ValueTask<ScheduledTaskRun?> GetRunAsync(string environment, string hostId, ScheduledTaskId scheduleId, ScheduledTaskId runId, DateTimeOffset intendedFireTimeUtc, CancellationToken cancellationToken = default);
    /// <summary>Reads an opaque-cursor page of retained occurrences for one schedule.</summary>
    ValueTask<ScheduledTaskRunPage> GetRunHistoryAsync(string environment, string hostId, ScheduledTaskId scheduleId, int pageSize, byte[]? pagingState, CancellationToken cancellationToken = default);
    /// <summary>Reads a bounded page of recent occurrences for one schedule.</summary>
    ValueTask<ScheduledTaskRun[]> GetRunsAsync(string environment, string hostId, ScheduledTaskId scheduleId, int limit, CancellationToken cancellationToken = default);
}
/// <summary>Writes read models only from committed scheduled-task source events.</summary>
public interface IScheduledTaskProjectionWriter
{
    /// <summary>Projects one definition with revision-fenced database timestamps.</summary>
    ValueTask ProjectAsync(ScheduledTaskDefinition definition, CancellationToken cancellationToken = default);
    /// <summary>Projects one catalog with revision-fenced database timestamps.</summary>
    ValueTask ProjectAsync(ScheduledTaskCatalog catalog, CancellationToken cancellationToken = default);
    /// <summary>Projects one run with revision-fenced database timestamps.</summary>
    ValueTask ProjectAsync(ScheduledTaskRun run, CancellationToken cancellationToken = default);
}
