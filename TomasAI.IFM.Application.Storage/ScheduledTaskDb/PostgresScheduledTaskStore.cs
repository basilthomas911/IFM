using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ScheduledTaskDb;
/// <summary>Native PostgreSQL scheduler projections, revision fencing and bounded keyset history.</summary>
internal sealed class PostgresScheduledTaskStore(string connectionString)
{
    internal async Task<DateOnly?> CompletedDate(string environment, CancellationToken token)
    {
        var rows = await (Read("SELECT completed_value_date FROM scheduled_task.futures_completed_end_of_day WHERE environment=$1;", [environment], row => row.GetFieldValue<DateOnly>(0), token)).ConfigureAwait(false);
        return rows.Count == 0 ? null : rows[0];
    }

    internal async Task<T?> Single<T>(string table, string predicate, object? [] values, CancellationToken token)
        where T : class
    {
        var rows = await (Read($"SELECT payload FROM scheduled_task.{table} WHERE {predicate};", values, row => MessagePackSerializer.Deserialize<T>(row.GetFieldValue<byte[]>(0)), token)).ConfigureAwait(false);
        return rows.FirstOrDefault();
    }

    internal async Task<T[]> List<T>(string table, string predicate, string order, object? [] values, CancellationToken token) => (await (Read($"SELECT payload FROM scheduled_task.{table} WHERE {predicate} {order} LIMIT ${values.Length};", values, row => MessagePackSerializer.Deserialize<T>(row.GetFieldValue<byte[]>(0)), token)).ConfigureAwait(false)).ToArray();
    internal async Task<ScheduledTaskRunPage> History(string environment, string host, ScheduledTaskId schedule, int size, byte[]? paging, CancellationToken token)
    {
        size = Math.Clamp(size, 1, 100);
        var scope = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { environment, host, schedule = schedule.Value })));
        var cursor = paging is null ? null : JsonSerializer.Deserialize<Cursor>(paging) ?? throw new ArgumentException("Invalid scheduled-task history cursor.", nameof(paging));
        if (cursor is not null && (!string.Equals(cursor.Scope, scope, StringComparison.Ordinal) || cursor.Fire.Kind != DateTimeKind.Utc))
            throw new ArgumentException("Scheduled-task cursor belongs to another query or has an invalid timestamp.", nameof(paging));
        object? [] values = cursor is null ? [environment, host, schedule.Value, size + 1] : [environment, host, schedule.Value, cursor.Fire, cursor.Run, size + 1];
        var predicate = cursor is null ? "" : "AND (intended_fire < $4 OR (intended_fire = $4 AND run_id > $5))";
        var rows = await (Read($"SELECT payload FROM scheduled_task.scheduled_task_run WHERE environment=$1 AND host_id=$2 AND schedule_id=$3 {predicate} ORDER BY intended_fire DESC, run_id ASC LIMIT ${values.Length};", values, row => MessagePackSerializer.Deserialize<ScheduledTaskRun>(row.GetFieldValue<byte[]>(0)), token)).ConfigureAwait(false);
        var items = rows.Take(size).ToArray();
        return new()
        {
            Runs = items.Select(run => run with { StandardOutputTail = "", StandardErrorTail = "" }).ToArray(),
            PagingState = rows.Count > size ? JsonSerializer.SerializeToUtf8Bytes(new Cursor(scope, items[^1].IntendedFireTimeUtc.UtcDateTime, items[^1].Id.Value)) : null
        };
    }

    sealed record Cursor(string Scope, DateTime Fire, Guid Run);
    internal Task Project(ScheduledTaskDefinition value, CancellationToken token) => Write(tx => Project(tx, value, token), token);
    internal Task Project(ScheduledTaskCatalog value, CancellationToken token) => Write(tx => Project(tx, value, token), token);
    internal Task Project(ScheduledTaskRun value, CancellationToken token) => Write(tx => Project(tx, value, token), token);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    internal Task<int> Project(IEnlistedPostgresTransaction transaction, ScheduledTaskDefinition value, CancellationToken token) => transaction.ExecuteAsync("INSERT INTO scheduled_task.scheduled_task_definition(environment,host_id,schedule_id,revision,payload) VALUES($1,$2,$3,$4,$5) ON CONFLICT(environment,host_id,schedule_id) DO UPDATE SET revision=EXCLUDED.revision,payload=EXCLUDED.payload WHERE scheduled_task_definition.revision <= EXCLUDED.revision;", [value.Schedule.Environment, value.Schedule.HostId, value.Id.Value, value.Revision, MessagePackSerializer.Serialize(value, cancellationToken: token)], token);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    internal Task<int> Project(IEnlistedPostgresTransaction transaction, ScheduledTaskCatalog value, CancellationToken token) => transaction.ExecuteAsync("INSERT INTO scheduled_task.scheduled_task_catalog(environment,host_id,revision,payload) VALUES($1,$2,$3,$4) ON CONFLICT(environment,host_id) DO UPDATE SET revision=EXCLUDED.revision,payload=EXCLUDED.payload WHERE scheduled_task_catalog.revision <= EXCLUDED.revision;", [value.Environment, value.HostId, value.Revision, MessagePackSerializer.Serialize(value, cancellationToken: token)], token);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    internal async Task Project(IEnlistedPostgresTransaction transaction, ScheduledTaskRun value, CancellationToken token)
    {
        var written = await (transaction.ExecuteAsync("INSERT INTO scheduled_task.scheduled_task_run(environment,host_id,schedule_id,intended_fire,run_id,revision,payload) VALUES($1,$2,$3,$4,$5,$6,$7) ON CONFLICT(environment,host_id,schedule_id,intended_fire,run_id) DO UPDATE SET revision=EXCLUDED.revision,payload=EXCLUDED.payload WHERE scheduled_task_run.revision <= EXCLUDED.revision;", [value.Environment, value.HostId, value.ScheduleId.Value, value.IntendedFireTimeUtc.UtcDateTime, value.Id.Value, value.Revision, MessagePackSerializer.Serialize(value, cancellationToken: token)], token)).ConfigureAwait(false);
        if (written != 0 && value.CompletedEndOfDayValueDate is { } date)
            await (transaction.ExecuteAsync("INSERT INTO scheduled_task.futures_completed_end_of_day(environment,completed_value_date,run_id) VALUES($1,$2,$3) ON CONFLICT(environment) DO UPDATE SET completed_value_date=EXCLUDED.completed_value_date,run_id=EXCLUDED.run_id WHERE futures_completed_end_of_day.completed_value_date <= EXCLUDED.completed_value_date;", [value.Environment, date, value.Id.Value], token)).ConfigureAwait(false);
    }

    async Task Write(Func<IEnlistedPostgresTransaction, Task> operation, CancellationToken token)
    {
        var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        await (connection.OpenAsync(token)).ConfigureAwait(false);
        var transaction = await (connection.BeginTransactionAsync(token)).ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        var enlisted = new LocalTransaction(connection, transaction);
        await (operation(enlisted)).ConfigureAwait(false);
        await (transaction.CommitAsync(token)).ConfigureAwait(false);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "This storage executor accepts application SQL from reviewed schema/query contracts; runtime values are sent as bound parameters rather than interpolated into SQL.")]
    async Task<List<T>> Read<T>(string sql, object? [] values, Func<NpgsqlDataReader, T> map, CancellationToken token)
    {
        var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        await (connection.OpenAsync(token)).ConfigureAwait(false);
        var command = new NpgsqlCommand(sql, connection);
        await using var commandDisposal = command.ConfigureAwait(false);
        Add(command, values);
        var reader = await (command.ExecuteReaderAsync(token)).ConfigureAwait(false);
        await using var readerDisposal = reader.ConfigureAwait(false);
        var rows = new List<T>();
        while (await (reader.ReadAsync(token)).ConfigureAwait(false))
            rows.Add(map(reader));
        return rows;
    }

    static void Add(NpgsqlCommand command, object? [] values)
    {
        foreach (var value in values)
            command.Parameters.Add(new NpgsqlParameter { Value = value ?? DBNull.Value });
    }

    sealed class LocalTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction) : IEnlistedPostgresTransaction
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "This storage executor accepts application SQL from reviewed schema/query contracts; runtime values are sent as bound parameters rather than interpolated into SQL.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        public async Task<int> ExecuteAsync(string sql, object? [] parameters, CancellationToken token)
        {
            var command = new NpgsqlCommand(sql, connection, transaction);
            await using var commandDisposal = command.ConfigureAwait(false);
            Add(command, parameters);
            return await (command.ExecuteNonQueryAsync(token)).ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "This storage executor accepts application SQL from reviewed schema/query contracts; runtime values are sent as bound parameters rather than interpolated into SQL.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        public async Task<object?> ScalarAsync(string sql, object? [] parameters, CancellationToken token)
        {
            var command = new NpgsqlCommand(sql, connection, transaction);
            await using var commandDisposal = command.ConfigureAwait(false);
            Add(command, parameters);
            return await (command.ExecuteScalarAsync(token)).ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "This storage executor accepts application SQL from reviewed schema/query contracts; runtime values are sent as bound parameters rather than interpolated into SQL.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? [] parameters, Func<NpgsqlDataReader, T> map, CancellationToken token)
        {
            var command = new NpgsqlCommand(sql, connection, transaction);
            await using var commandDisposal = command.ConfigureAwait(false);
            Add(command, parameters);
            var reader = await (command.ExecuteReaderAsync(token)).ConfigureAwait(false);
            await using var readerDisposal = reader.ConfigureAwait(false);
            var rows = new List<T>();
            while (await (reader.ReadAsync(token)).ConfigureAwait(false))
                rows.Add(map(reader));
            return rows;
        }
    }
}