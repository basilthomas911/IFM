using MessagePack;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
namespace TomasAI.IFM.Application.Storage.ScheduledTaskDb;
/// <summary>Persists and reads host-partitioned administrative models in ScyllaDB.</summary>
public sealed class ScheduledTaskReadStore(IDbConnectionSettings settings, ILogger<DbProvider> logger)
    : ObjectDataRepository<ScheduledTaskReadStore>(settings["TradeDbConnection"], logger), IScheduledTaskReadStore, IScheduledTaskProjectionWriter
{
    /// <inheritdoc />
    public override ScheduledTaskReadStore Database => this;
    /// <inheritdoc />
    public async ValueTask<DateOnly?> GetCompletedEndOfDayAsync(string environment, CancellationToken cancellationToken = default)
    {
        var result = await Use("ScheduledTask.CompletedEndOfDay", "SELECT completed_value_date FROM futures_completed_end_of_day WHERE environment=:Environment;")
            .SetParameters(new { Environment = environment })
            .ExecuteSingleAsync(row => new CompletedDate(row.GetDateOnly(0)), cancellationToken).ConfigureAwait(false);
        return result?.ValueDate;
    }
    private sealed record CompletedDate(DateOnly ValueDate);
    /// <inheritdoc />
    public async ValueTask<ScheduledTaskDefinition?> GetDefinitionAsync(string environment, string hostId, ScheduledTaskId id, CancellationToken cancellationToken = default) =>
        await Use("ScheduledTask.GetDefinition", "SELECT payload FROM scheduled_task_definition WHERE environment=:Environment AND host_id=:HostId AND schedule_id=:Id;")
        .SetParameters(new { Environment = environment, HostId = hostId, Id = id.Value })
        .ExecuteSingleAsync(row => MessagePackSerializer.Deserialize<ScheduledTaskDefinition>(row.GetBytes(0)), cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    public async ValueTask<ScheduledTaskDefinition[]> GetDefinitionsAsync(string environment, string hostId, int limit, CancellationToken cancellationToken = default) =>
        (await Use("ScheduledTask.ListDefinitions", "SELECT payload FROM scheduled_task_definition WHERE environment=:Environment AND host_id=:HostId LIMIT :RowCount;")
        .SetParameters(new { Environment = environment, HostId = hostId, RowCount = Math.Clamp(limit, 1, 500) })
        .ExecuteQueryAsync(row => MessagePackSerializer.Deserialize<ScheduledTaskDefinition>(row.GetBytes(0)), cancellationToken).ConfigureAwait(false)).ToArray();
    /// <inheritdoc />
    public async ValueTask<ScheduledTaskCatalog?> GetCatalogAsync(string environment, string hostId, CancellationToken cancellationToken = default) =>
        await Use("ScheduledTask.GetCatalog", "SELECT payload FROM scheduled_task_catalog WHERE environment=:Environment AND host_id=:HostId;")
        .SetParameters(new { Environment = environment, HostId = hostId })
        .ExecuteSingleAsync(row => MessagePackSerializer.Deserialize<ScheduledTaskCatalog>(row.GetBytes(0)), cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    public async ValueTask<ScheduledTaskRun?> GetRunAsync(string environment, string hostId, ScheduledTaskId scheduleId, ScheduledTaskId runId, DateTimeOffset intendedFireTimeUtc, CancellationToken cancellationToken = default) =>
        await Use("ScheduledTask.GetRun", "SELECT payload FROM scheduled_task_run WHERE environment=:Environment AND host_id=:HostId AND schedule_id=:ScheduleId AND intended_fire=:Fire AND run_id=:RunId;")
        .SetParameters(new { Environment = environment, HostId = hostId, ScheduleId = scheduleId.Value, Fire = intendedFireTimeUtc.UtcDateTime, RunId = runId.Value })
        .ExecuteSingleAsync(row => MessagePackSerializer.Deserialize<ScheduledTaskRun>(row.GetBytes(0)), cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    public async ValueTask<ScheduledTaskRunPage> GetRunHistoryAsync(string environment, string hostId, ScheduledTaskId scheduleId, int pageSize, byte[]? pagingState, CancellationToken cancellationToken = default)
    {
        var page = await Use("ScheduledTask.RunHistory", "SELECT payload FROM scheduled_task_run WHERE environment=:Environment AND host_id=:HostId AND schedule_id=:ScheduleId;")
            .SetParameters(new { Environment = environment, HostId = hostId, ScheduleId = scheduleId.Value })
            .ExecutePageAsync(row => MessagePackSerializer.Deserialize<ScheduledTaskRun>(row.GetBytes(0)), Math.Clamp(pageSize, 1, 100), pagingState, cancellationToken);
        return new() { Runs = page.Items.Select(run => run with { StandardOutputTail = "", StandardErrorTail = "" }).ToArray(), PagingState = page.PagingState };
    }
    /// <inheritdoc />
    public async ValueTask<ScheduledTaskRun[]> GetRunsAsync(string environment, string hostId, ScheduledTaskId scheduleId, int limit, CancellationToken cancellationToken = default) =>
        (await Use("ScheduledTask.ListRuns", "SELECT payload FROM scheduled_task_run WHERE environment=:Environment AND host_id=:HostId AND schedule_id=:ScheduleId LIMIT :RowCount;")
        .SetParameters(new { Environment = environment, HostId = hostId, ScheduleId = scheduleId.Value, RowCount = Math.Clamp(limit, 1, 100) })
        .ExecuteQueryAsync(row => MessagePackSerializer.Deserialize<ScheduledTaskRun>(row.GetBytes(0)), cancellationToken).ConfigureAwait(false)).ToArray();
    /// <inheritdoc />
    public async ValueTask ProjectAsync(ScheduledTaskDefinition definition, CancellationToken cancellationToken = default) =>
        await Use("ScheduledTask.ProjectDefinition", "INSERT INTO scheduled_task_definition (environment,host_id,schedule_id,revision,payload) VALUES (:Environment,:HostId,:Id,:Revision,:Payload) USING TIMESTAMP :Revision;")
        .SetParameters(new { definition.Schedule.Environment, definition.Schedule.HostId, Id = definition.Id.Value, definition.Revision, Payload = MessagePackSerializer.Serialize(definition) }).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    public async ValueTask ProjectAsync(ScheduledTaskCatalog catalog, CancellationToken cancellationToken = default) =>
        await Use("ScheduledTask.ProjectCatalog", "INSERT INTO scheduled_task_catalog (environment,host_id,revision,payload) VALUES (:Environment,:HostId,:Revision,:Payload) USING TIMESTAMP :Revision;")
        .SetParameters(new { catalog.Environment, catalog.HostId, catalog.Revision, Payload = MessagePackSerializer.Serialize(catalog) }).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    public async ValueTask ProjectAsync(ScheduledTaskRun run, CancellationToken cancellationToken = default)
    {
        await Use("ScheduledTask.ProjectRun", "INSERT INTO scheduled_task_run (environment,host_id,schedule_id,intended_fire,run_id,revision,payload) VALUES (:Environment,:HostId,:ScheduleId,:Fire,:Id,:Revision,:Payload) USING TIMESTAMP :Revision;")
        .SetParameters(new { run.Environment, run.HostId, ScheduleId = run.ScheduleId.Value, Fire = run.IntendedFireTimeUtc.UtcDateTime, Id = run.Id.Value, run.Revision, Payload = MessagePackSerializer.Serialize(run) }).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        if (run.CompletedEndOfDayValueDate is { } completedDate)
            await Use("ScheduledTask.ProjectCompletedEndOfDay", "INSERT INTO futures_completed_end_of_day (environment,completed_value_date,run_id) VALUES (:Environment,:ValueDate,:RunId) USING TIMESTAMP :DateRevision;")
                .SetParameters(new { run.Environment, ValueDate = completedDate, RunId = run.Id.Value, DateRevision = (long)completedDate.DayNumber })
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }
}
