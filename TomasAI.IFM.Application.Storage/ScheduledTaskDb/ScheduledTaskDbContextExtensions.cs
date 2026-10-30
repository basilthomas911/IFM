using System.Security.Cryptography;
using System.Text.Json;
using MessagePack;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Framework.Storage.Postgres;

namespace TomasAI.IFM.Application.Storage.ScheduledTaskDb;
/// <summary>Technical cursor, revision fencing and transaction resource helpers.</summary>
internal static class ScheduledTaskDbContextExtensions
{
    extension(ScheduledTaskDbContext context)
    {
        /// <summary>Rejects a missing snapshot or transaction before persistence.</summary>
        internal void ValidateSnapshot(object value) => ArgumentNullException.ThrowIfNull(value);
        /// <summary>Reads a scoped keyset page and strips output tails from history rows.</summary>
        internal async Task<ScheduledTaskRunPage> ReadHistoryAsync(string environment, string host, ScheduledTaskId schedule, int size, byte[]? paging, CancellationToken token)
        {
            size = Math.Clamp(size, 1, 100);
            var scope = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { environment, host, schedule = schedule.Value })));
            var cursor = paging is null ? null : JsonSerializer.Deserialize<ScheduledTaskHistoryCursor>(paging) ?? throw new ArgumentException("Invalid scheduled-task history cursor.", nameof(paging));
            if (cursor is not null && (!string.Equals(cursor.Scope, scope, StringComparison.Ordinal) || cursor.Fire.Kind != DateTimeKind.Utc))
                throw new ArgumentException("Scheduled-task cursor belongs to another query or has an invalid timestamp.", nameof(paging));
            object? [] values = cursor is null ? [environment, host, schedule.Value, size + 1] : [environment, host, schedule.Value, cursor.Fire, cursor.Run, size + 1];
            var rows = await context.Use("ScheduledTask.History", cursor is null ? ScheduledTaskDbSql.History : ScheduledTaskDbSql.HistoryAfter)
            .SetParameters(new ScheduledTaskDbParameters(values))
            .ExecuteQueryAsync(ScheduledTaskDbContext.MapToSnapshot<ScheduledTaskRun>, token)
            .ConfigureAwait(false);
            var items = rows.Take(size).ToArray();
            return new()
            {
                Runs = items.Select(run => run with { StandardOutputTail = "", StandardErrorTail = "" }).ToArray(),
                PagingState = rows.Count > size ? JsonSerializer.SerializeToUtf8Bytes(new ScheduledTaskHistoryCursor(scope, items[^1].IntendedFireTimeUtc.UtcDateTime, items[^1].Id.Value)) : null
            };
        }

        /// <summary>Owns the local projection transaction and commits only after all writes succeed.</summary>
        internal async Task WriteSnapshotAsync(Func<IEnlistedPostgresTransaction, Task> operation, CancellationToken token)
        {
            var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(context.ConnectionString);
            await using var connectionDisposal = connection.ConfigureAwait(false);
            await (connection.OpenAsync(token))
            .ConfigureAwait(false);
            var transaction = await (connection.BeginTransactionAsync(token))
            .ConfigureAwait(false);
            await using var transactionDisposal = transaction.ConfigureAwait(false);
            var enlisted = new ScheduledTaskLocalTransaction(connection, transaction);
            await (operation(enlisted))
            .ConfigureAwait(false);
            await (transaction.CommitAsync(token))
            .ConfigureAwait(false);
        }
    }

    extension(IEnlistedPostgresTransaction transaction)
    {
        /// <summary>Upserts an accepted snapshot with persisted revision fencing.</summary>
        internal Task<int> ProjectScheduledTaskAsync(ScheduledTaskDefinition value, CancellationToken token) => transaction.ExecuteAsync(ScheduledTaskDbSql.UpsertDefinition, [value.Schedule.Environment, value.Schedule.HostId, value.Id.Value, value.Revision, MessagePackSerializer.Serialize(value, cancellationToken: token)], token);
        /// <summary>Upserts an accepted snapshot with persisted revision fencing.</summary>
        internal Task<int> ProjectScheduledTaskAsync(ScheduledTaskCatalog value, CancellationToken token) => transaction.ExecuteAsync(ScheduledTaskDbSql.UpsertCatalog, [value.Environment, value.HostId, value.Revision, MessagePackSerializer.Serialize(value, cancellationToken: token)], token);
        /// <summary>Atomically upserts a run and advances its completed session date.</summary>
        internal async Task ProjectScheduledTaskAsync(ScheduledTaskRun value, CancellationToken token)
        {
            var written = await (transaction.ExecuteAsync(ScheduledTaskDbSql.UpsertRun, [value.Environment, value.HostId, value.ScheduleId.Value, value.IntendedFireTimeUtc.UtcDateTime, value.Id.Value, value.Revision, MessagePackSerializer.Serialize(value, cancellationToken: token)], token))
            .ConfigureAwait(false);
            if (written != 0 && value.CompletedEndOfDayValueDate is { } date)
                await (transaction.ExecuteAsync(ScheduledTaskDbSql.UpsertCompletedDate, [value.Environment, date, value.Id.Value], token))
            .ConfigureAwait(false);
        }
    }

    extension(NpgsqlCommand command)
    {
        /// <summary>Binds positional values without interpolating runtime data into SQL.</summary>
        internal void BindScheduledTaskParameters(object? [] values)
        {
            foreach (var value in values)
                command.Parameters.Add(value);
        }
    }
}

/// <summary>Opaque query-scoped history keyset.</summary>
internal sealed record ScheduledTaskHistoryCursor(string Scope, DateTime Fire, Guid Run);
/// <summary>Adapter that never owns commit and binds operations to the local projection transaction.</summary>
internal sealed class ScheduledTaskLocalTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction) : IEnlistedPostgresTransaction
{
    readonly PostgresQueryTimeoutScope timeouts = new(connection, transaction);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "This storage executor accepts application SQL from reviewed schema/query contracts; runtime values are sent as bound parameters rather than interpolated into SQL.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<int> ExecuteAsync(string sql, object? [] parameters, CancellationToken token, PostgresQueryTimeout? timeout = null)
    {
        await timeouts.ApplyAsync(timeout, token).ConfigureAwait(false);
        var command = new NpgsqlCommand(sql, connection, transaction) { CommandTimeout = 0 };
        await using var commandDisposal = command.ConfigureAwait(false);
        command.BindScheduledTaskParameters(parameters);
        return await (command.ExecuteNonQueryAsync(token))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "This storage executor accepts application SQL from reviewed schema/query contracts; runtime values are sent as bound parameters rather than interpolated into SQL.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<object?> ScalarAsync(string sql, object? [] parameters, CancellationToken token, PostgresQueryTimeout? timeout = null)
    {
        await timeouts.ApplyAsync(timeout, token).ConfigureAwait(false);
        var command = new NpgsqlCommand(sql, connection, transaction) { CommandTimeout = 0 };
        await using var commandDisposal = command.ConfigureAwait(false);
        command.BindScheduledTaskParameters(parameters);
        return await (command.ExecuteScalarAsync(token))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "This storage executor accepts application SQL from reviewed schema/query contracts; runtime values are sent as bound parameters rather than interpolated into SQL.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? [] parameters, Func<System.Data.Common.DbDataReader, T> map, CancellationToken token, PostgresQueryTimeout? timeout = null)
    {
        await timeouts.ApplyAsync(timeout, token).ConfigureAwait(false);
        var command = new NpgsqlCommand(sql, connection, transaction) { CommandTimeout = 0 };
        await using var commandDisposal = command.ConfigureAwait(false);
        command.BindScheduledTaskParameters(parameters);
        var reader = await (command.ExecuteReaderAsync(token))
            .ConfigureAwait(false);
        await using var readerDisposal = reader.ConfigureAwait(false);
        var rows = new List<T>();
        while (await (reader.ReadAsync(token))
            .ConfigureAwait(false))
            rows.Add(map(reader));
        return rows;
    }
}