using MessagePack;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
namespace TomasAI.IFM.Application.Storage.ScheduledTaskDb;
/// <summary>Persists and reads scheduled-task projections using the configured provider.</summary>
public sealed class ScheduledTaskReadStore(IDbConnectionSettings settings, ILogger<DbProvider> logger)
    : ObjectDataRepository<ScheduledTaskReadStore>(settings[ConnectionName] ?? settings["TradeDbConnection"], logger), IScheduledTaskReadStore, IScheduledTaskProjectionWriter, IScheduledTaskTransactionalProjectionWriter
{
    public const string ConnectionName = "ScheduledTaskDbConnection";
    readonly PostgresScheduledTaskStore? _postgres = string.Equals((settings[ConnectionName] ?? settings["TradeDbConnection"])?.ProviderName,"System.Data.Postgres",StringComparison.Ordinal)
        ? new((settings[ConnectionName] ?? settings["TradeDbConnection"]).ConnectionString) : null;

    public ValueTask<DateOnly?> GetCompletedEndOfDayAsync(string environment, CancellationToken cancellationToken = default) =>
        _postgres is null ? GetCompletedEndOfDayScyllaAsync(environment, cancellationToken) : new(_postgres.CompletedDate(environment, cancellationToken));
    public ValueTask<ScheduledTaskDefinition?> GetDefinitionAsync(string environment, string hostId, ScheduledTaskId id, CancellationToken cancellationToken = default) =>
        _postgres is null ? GetDefinitionScyllaAsync(environment, hostId, id, cancellationToken) : new(_postgres.Single<ScheduledTaskDefinition>("scheduled_task_definition", "environment=$1 AND host_id=$2 AND schedule_id=$3", [environment, hostId, id.Value], cancellationToken));
    public ValueTask<ScheduledTaskDefinition[]> GetDefinitionsAsync(string environment, string hostId, int limit, CancellationToken cancellationToken = default) =>
        _postgres is null ? GetDefinitionsScyllaAsync(environment, hostId, limit, cancellationToken) : new(_postgres.List<ScheduledTaskDefinition>("scheduled_task_definition", "environment=$1 AND host_id=$2", "ORDER BY schedule_id", [environment, hostId, Math.Clamp(limit, 1, 500)], cancellationToken));
    public ValueTask<ScheduledTaskCatalog?> GetCatalogAsync(string environment, string hostId, CancellationToken cancellationToken = default) =>
        _postgres is null ? GetCatalogScyllaAsync(environment, hostId, cancellationToken) : new(_postgres.Single<ScheduledTaskCatalog>("scheduled_task_catalog", "environment=$1 AND host_id=$2", [environment, hostId], cancellationToken));
    public ValueTask<ScheduledTaskRun?> GetRunAsync(string environment, string hostId, ScheduledTaskId scheduleId, ScheduledTaskId runId, DateTimeOffset intendedFireTimeUtc, CancellationToken cancellationToken = default) =>
        _postgres is null ? GetRunScyllaAsync(environment, hostId, scheduleId, runId, intendedFireTimeUtc, cancellationToken) : new(_postgres.Single<ScheduledTaskRun>("scheduled_task_run", "environment=$1 AND host_id=$2 AND schedule_id=$3 AND intended_fire=$4 AND run_id=$5", [environment, hostId, scheduleId.Value, intendedFireTimeUtc.UtcDateTime, runId.Value], cancellationToken));
    public ValueTask<ScheduledTaskRunPage> GetRunHistoryAsync(string environment, string hostId, ScheduledTaskId scheduleId, int pageSize, byte[]? pagingState, CancellationToken cancellationToken = default) =>
        _postgres is null ? GetRunHistoryScyllaAsync(environment, hostId, scheduleId, pageSize, pagingState, cancellationToken) : new(_postgres.History(environment, hostId, scheduleId, pageSize, pagingState, cancellationToken));
    public ValueTask<ScheduledTaskRun[]> GetRunsAsync(string environment, string hostId, ScheduledTaskId scheduleId, int limit, CancellationToken cancellationToken = default) =>
        _postgres is null ? GetRunsScyllaAsync(environment, hostId, scheduleId, limit, cancellationToken) : new(_postgres.List<ScheduledTaskRun>("scheduled_task_run", "environment=$1 AND host_id=$2 AND schedule_id=$3", "ORDER BY intended_fire DESC,run_id ASC", [environment, hostId, scheduleId.Value, Math.Clamp(limit, 1, 100)], cancellationToken));
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public ValueTask ProjectAsync(ScheduledTaskDefinition value, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(value);
        return _postgres is null ? ProjectScyllaAsync(value, cancellationToken) : new(_postgres.Project(value, cancellationToken));
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public ValueTask ProjectAsync(ScheduledTaskCatalog value, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(value);
        return _postgres is null ? ProjectScyllaAsync(value, cancellationToken) : new(_postgres.Project(value, cancellationToken));
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public ValueTask ProjectAsync(ScheduledTaskRun value, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(value);
        return _postgres is null ? ProjectScyllaAsync(value, cancellationToken) : new(_postgres.Project(value, cancellationToken));
    }

    public Task ProjectAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction, ScheduledTaskDefinition value, CancellationToken token) 
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(value);
        return (_postgres ?? throw new InvalidOperationException("Enlisted scheduled-task writes require PostgreSQL.")).Project(transaction, value, token);
    }
    public Task ProjectAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction, ScheduledTaskCatalog value, CancellationToken token) 
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(value);
        return (_postgres ?? throw new InvalidOperationException("Enlisted scheduled-task writes require PostgreSQL.")).Project(transaction, value, token);
    }
    public Task ProjectAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction, ScheduledTaskRun value, CancellationToken token) 
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(value);
        return (_postgres ?? throw new InvalidOperationException("Enlisted scheduled-task writes require PostgreSQL.")).Project(transaction, value, token);
    }

    /// <inheritdoc />
    public override ScheduledTaskReadStore Database => this;
    /// <inheritdoc />
    private async ValueTask<DateOnly?> GetCompletedEndOfDayScyllaAsync(string environment, CancellationToken cancellationToken = default)
    {
        var result = await Use("ScheduledTask.CompletedEndOfDay", "SELECT completed_value_date FROM futures_completed_end_of_day WHERE environment=:Environment;")
            .SetParameters(new { Environment = environment })
            .ExecuteSingleAsync(row => new CompletedDate(row.GetDateOnly(0)), cancellationToken).ConfigureAwait(false);
        return result?.ValueDate;
    }
    private sealed record CompletedDate(DateOnly ValueDate);
    /// <inheritdoc />
    private async ValueTask<ScheduledTaskDefinition?> GetDefinitionScyllaAsync(string environment, string hostId, ScheduledTaskId id, CancellationToken cancellationToken = default) =>
        await Use("ScheduledTask.GetDefinition", "SELECT payload FROM scheduled_task_definition WHERE environment=:Environment AND host_id=:HostId AND schedule_id=:Id;")
        .SetParameters(new { Environment = environment, HostId = hostId, Id = id.Value })
        .ExecuteSingleAsync(row => MessagePackSerializer.Deserialize<ScheduledTaskDefinition>(row.GetBytes(0)), cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    private async ValueTask<ScheduledTaskDefinition[]> GetDefinitionsScyllaAsync(string environment, string hostId, int limit, CancellationToken cancellationToken = default) =>
        (await Use("ScheduledTask.ListDefinitions", "SELECT payload FROM scheduled_task_definition WHERE environment=:Environment AND host_id=:HostId LIMIT :RowCount;")
        .SetParameters(new { Environment = environment, HostId = hostId, RowCount = Math.Clamp(limit, 1, 500) })
        .ExecuteQueryAsync(row => MessagePackSerializer.Deserialize<ScheduledTaskDefinition>(row.GetBytes(0)), cancellationToken).ConfigureAwait(false)).ToArray();
    /// <inheritdoc />
    private async ValueTask<ScheduledTaskCatalog?> GetCatalogScyllaAsync(string environment, string hostId, CancellationToken cancellationToken = default) =>
        await Use("ScheduledTask.GetCatalog", "SELECT payload FROM scheduled_task_catalog WHERE environment=:Environment AND host_id=:HostId;")
        .SetParameters(new { Environment = environment, HostId = hostId })
        .ExecuteSingleAsync(row => MessagePackSerializer.Deserialize<ScheduledTaskCatalog>(row.GetBytes(0)), cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    private async ValueTask<ScheduledTaskRun?> GetRunScyllaAsync(string environment, string hostId, ScheduledTaskId scheduleId, ScheduledTaskId runId, DateTimeOffset intendedFireTimeUtc, CancellationToken cancellationToken = default) =>
        await Use("ScheduledTask.GetRun", "SELECT payload FROM scheduled_task_run WHERE environment=:Environment AND host_id=:HostId AND schedule_id=:ScheduleId AND intended_fire=:Fire AND run_id=:RunId;")
        .SetParameters(new { Environment = environment, HostId = hostId, ScheduleId = scheduleId.Value, Fire = intendedFireTimeUtc.UtcDateTime, RunId = runId.Value })
        .ExecuteSingleAsync(row => MessagePackSerializer.Deserialize<ScheduledTaskRun>(row.GetBytes(0)), cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    private async ValueTask<ScheduledTaskRunPage> GetRunHistoryScyllaAsync(string environment, string hostId, ScheduledTaskId scheduleId, int pageSize, byte[]? pagingState, CancellationToken cancellationToken = default)
    {
        var page = await (Use("ScheduledTask.RunHistory", "SELECT payload FROM scheduled_task_run WHERE environment=:Environment AND host_id=:HostId AND schedule_id=:ScheduleId;")
            .SetParameters(new { Environment = environment, HostId = hostId, ScheduleId = scheduleId.Value })
            .ExecutePageAsync(row => MessagePackSerializer.Deserialize<ScheduledTaskRun>(row.GetBytes(0)), Math.Clamp(pageSize, 1, 100), pagingState, cancellationToken)).ConfigureAwait(false);
        return new() { Runs = page.Items.Select(run => run with { StandardOutputTail = "", StandardErrorTail = "" }).ToArray(), PagingState = page.PagingState };
    }
    /// <inheritdoc />
    private async ValueTask<ScheduledTaskRun[]> GetRunsScyllaAsync(string environment, string hostId, ScheduledTaskId scheduleId, int limit, CancellationToken cancellationToken = default) =>
        (await Use("ScheduledTask.ListRuns", "SELECT payload FROM scheduled_task_run WHERE environment=:Environment AND host_id=:HostId AND schedule_id=:ScheduleId LIMIT :RowCount;")
        .SetParameters(new { Environment = environment, HostId = hostId, ScheduleId = scheduleId.Value, RowCount = Math.Clamp(limit, 1, 100) })
        .ExecuteQueryAsync(row => MessagePackSerializer.Deserialize<ScheduledTaskRun>(row.GetBytes(0)), cancellationToken).ConfigureAwait(false)).ToArray();
    /// <inheritdoc />
    private async ValueTask ProjectScyllaAsync(ScheduledTaskDefinition definition, CancellationToken cancellationToken = default) =>
        await Use("ScheduledTask.ProjectDefinition", "INSERT INTO scheduled_task_definition (environment,host_id,schedule_id,revision,payload) VALUES (:Environment,:HostId,:Id,:Revision,:Payload) USING TIMESTAMP :Revision;")
        .SetParameters(new { definition.Schedule.Environment, definition.Schedule.HostId, Id = definition.Id.Value, definition.Revision, Payload = MessagePackSerializer.Serialize(definition,cancellationToken:cancellationToken) }).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    private async ValueTask ProjectScyllaAsync(ScheduledTaskCatalog catalog, CancellationToken cancellationToken = default) =>
        await Use("ScheduledTask.ProjectCatalog", "INSERT INTO scheduled_task_catalog (environment,host_id,revision,payload) VALUES (:Environment,:HostId,:Revision,:Payload) USING TIMESTAMP :Revision;")
        .SetParameters(new { catalog.Environment, catalog.HostId, catalog.Revision, Payload = MessagePackSerializer.Serialize(catalog,cancellationToken:cancellationToken) }).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    private async ValueTask ProjectScyllaAsync(ScheduledTaskRun run, CancellationToken cancellationToken = default)
    {
        await Use("ScheduledTask.ProjectRun", "INSERT INTO scheduled_task_run (environment,host_id,schedule_id,intended_fire,run_id,revision,payload) VALUES (:Environment,:HostId,:ScheduleId,:Fire,:Id,:Revision,:Payload) USING TIMESTAMP :Revision;")
        .SetParameters(new { run.Environment, run.HostId, ScheduleId = run.ScheduleId.Value, Fire = run.IntendedFireTimeUtc.UtcDateTime, Id = run.Id.Value, run.Revision, Payload = MessagePackSerializer.Serialize(run,cancellationToken:cancellationToken) }).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        if (run.CompletedEndOfDayValueDate is { } completedDate)
            await Use("ScheduledTask.ProjectCompletedEndOfDay", "INSERT INTO futures_completed_end_of_day (environment,completed_value_date,run_id) VALUES (:Environment,:ValueDate,:RunId) USING TIMESTAMP :DateRevision;")
                .SetParameters(new { run.Environment, ValueDate = completedDate, RunId = run.Id.Value, DateRevision = (long)completedDate.DayNumber })
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }
}
