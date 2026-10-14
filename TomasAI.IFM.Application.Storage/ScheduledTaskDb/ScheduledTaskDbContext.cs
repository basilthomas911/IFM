using MessagePack;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.ScheduledTaskDb;
/// <summary>Typed PostgreSQL scheduled-task persistence; domain actors own scheduling and lifecycle policy.</summary>
/// <param name = "settings">Named database connection settings.</param>
/// <param name = "logger">The repository logger.</param>
public sealed class ScheduledTaskDbContext(IDbConnectionSettings settings, ILogger<DbProvider> logger) : ObjectDataRepository<ScheduledTaskDbContext>(settings[ConnectionName] ?? settings["TradeDbConnection"], logger), IScheduledTaskDbContext
{
    /// <summary>The named scheduled-task PostgreSQL connection.</summary>
    public const string ConnectionName = "ScheduledTaskDbConnection";
    /// <inheritdoc/>
    public override ScheduledTaskDbContext Database => this;

    /// <summary>Maps a persisted MessagePack snapshot from the payload column.</summary>
    internal static T MapToSnapshot<T>(IObjectDataRecord row) => MessagePackSerializer.Deserialize<T>(row.GetBytes(0));
    /// <summary>Maps the persisted completed session date.</summary>
    internal static DateOnly MapToCompletedDate(IObjectDataRecord row) => row.GetDateOnly(0);
    /// <inheritdoc/>
    public async ValueTask<DateOnly?> GetCompletedEndOfDayAsync(string environment, CancellationToken cancellationToken = default)
    {
        var rows = await Use("ScheduledTask.CompletedDate", ScheduledTaskDbSql.CompletedDate)
            .SetParameters(new ScheduledTaskDbParameters([environment]))
            .ExecuteQueryAsync(MapToCompletedDate, cancellationToken)
            .ConfigureAwait(false);
        return rows.Count == 0 ? null : rows.First();
    }

    /// <inheritdoc/>
    public async ValueTask<ScheduledTaskDefinition?> GetDefinitionAsync(string environment, string hostId, ScheduledTaskId id, CancellationToken cancellationToken = default) => await Use("ScheduledTask.Definition", ScheduledTaskDbSql.Definition)
            .SetParameters(new ScheduledTaskDbParameters([environment, hostId, id.Value]))
            .ExecuteSingleAsync(MapToSnapshot<ScheduledTaskDefinition>, cancellationToken)
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async ValueTask<ScheduledTaskDefinition[]> GetDefinitionsAsync(string environment, string hostId, int limit, CancellationToken cancellationToken = default) => (await Use("ScheduledTask.Definitions", ScheduledTaskDbSql.Definitions)
            .SetParameters(new ScheduledTaskDbParameters([environment, hostId, Math.Clamp(limit, 1, 500)]))
            .ExecuteQueryAsync(MapToSnapshot<ScheduledTaskDefinition>, cancellationToken)
            .ConfigureAwait(false)).ToArray();
    /// <inheritdoc/>
    public async ValueTask<ScheduledTaskCatalog?> GetCatalogAsync(string environment, string hostId, CancellationToken cancellationToken = default) => await Use("ScheduledTask.Catalog", ScheduledTaskDbSql.Catalog)
            .SetParameters(new ScheduledTaskDbParameters([environment, hostId]))
            .ExecuteSingleAsync(MapToSnapshot<ScheduledTaskCatalog>, cancellationToken)
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async ValueTask<ScheduledTaskRun?> GetRunAsync(string environment, string hostId, ScheduledTaskId scheduleId, ScheduledTaskId runId, DateTimeOffset intendedFireTimeUtc, CancellationToken cancellationToken = default) => await Use("ScheduledTask.Run", ScheduledTaskDbSql.Run)
            .SetParameters(new ScheduledTaskDbParameters([environment, hostId, scheduleId.Value, intendedFireTimeUtc.UtcDateTime, runId.Value]))
            .ExecuteSingleAsync(MapToSnapshot<ScheduledTaskRun>, cancellationToken)
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async ValueTask<ScheduledTaskRun[]> GetRunsAsync(string environment, string hostId, ScheduledTaskId scheduleId, int limit, CancellationToken cancellationToken = default) => (await Use("ScheduledTask.Runs", ScheduledTaskDbSql.Runs)
            .SetParameters(new ScheduledTaskDbParameters([environment, hostId, scheduleId.Value, Math.Clamp(limit, 1, 100)]))
            .ExecuteQueryAsync(MapToSnapshot<ScheduledTaskRun>, cancellationToken)
            .ConfigureAwait(false)).ToArray();
    /// <inheritdoc/>
    public ValueTask<ScheduledTaskRunPage> GetRunHistoryAsync(string environment, string hostId, ScheduledTaskId scheduleId, int pageSize, byte[]? pagingState, CancellationToken cancellationToken = default) => new(this.ReadHistoryAsync(environment, hostId, scheduleId, pageSize, pagingState, cancellationToken));
    /// <inheritdoc/>
    public ValueTask ProjectAsync(ScheduledTaskDefinition definition, CancellationToken cancellationToken = default)
    {
        this.ValidateSnapshot(definition);
        return new(this.WriteSnapshotAsync(transaction => transaction.ProjectScheduledTaskAsync(definition, cancellationToken), cancellationToken));
    }

    /// <inheritdoc/>
    public Task ProjectAsync(IEnlistedPostgresTransaction transaction, ScheduledTaskDefinition value, CancellationToken token)
    {
        this.ValidateSnapshot(transaction);
        this.ValidateSnapshot(value);
        return transaction.ProjectScheduledTaskAsync(value, token);
    }

    /// <inheritdoc/>
    public ValueTask ProjectAsync(ScheduledTaskCatalog catalog, CancellationToken cancellationToken = default)
    {
        this.ValidateSnapshot(catalog);
        return new(this.WriteSnapshotAsync(transaction => transaction.ProjectScheduledTaskAsync(catalog, cancellationToken), cancellationToken));
    }

    /// <inheritdoc/>
    public Task ProjectAsync(IEnlistedPostgresTransaction transaction, ScheduledTaskCatalog value, CancellationToken token)
    {
        this.ValidateSnapshot(transaction);
        this.ValidateSnapshot(value);
        return transaction.ProjectScheduledTaskAsync(value, token);
    }

    /// <inheritdoc/>
    public ValueTask ProjectAsync(ScheduledTaskRun run, CancellationToken cancellationToken = default)
    {
        this.ValidateSnapshot(run);
        return new(this.WriteSnapshotAsync(transaction => transaction.ProjectScheduledTaskAsync(run, cancellationToken), cancellationToken));
    }

    /// <inheritdoc/>
    public Task ProjectAsync(IEnlistedPostgresTransaction transaction, ScheduledTaskRun value, CancellationToken token)
    {
        this.ValidateSnapshot(transaction);
        this.ValidateSnapshot(value);
        return transaction.ProjectScheduledTaskAsync(value, token);
    }
}