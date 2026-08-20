using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.CommandLogBenchmark;

/// <summary>
/// Adapter over the existing PostgreSQL command_log table and atomic ON CONFLICT guard.
/// It preserves the current JSON/text representation and remains the runtime authority.
/// </summary>
public sealed class PostgresCommandLogBenchmarkStore(
    IDbConnectionSettings connectionSettings,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<PostgresCommandLogBenchmarkStore>(
        connectionSettings[EventSourceActorDbContext.EventSourceActorDbConnection],
        logger),
      ICommandLogBenchmarkStore
{
    public override IObjectRepository Database => this;

    public async Task CreateSchemaAsync(CancellationToken cancellationToken = default)
        => _ = await Database.Use(EventSourceSchemaSql.CreateCommandLog)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> TryInsertAsync(
        CommandLogBenchmarkEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return await Database.Use(EventSourceDbSql.TryInsertCommandLog)
            .SetParameters(new InsertParameters(entry))
            .ExecuteScalarAsync(static row => row.GetBool(0), cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<PostgresCommandLogBenchmarkRecord?> GetAsync(
        Guid commandId,
        CancellationToken cancellationToken = default)
        => Database.Use(CommandLogBenchmarkStatements.GetPostgres)
            .SetParameters(new CommandIdParameter(commandId))
            .ExecuteSingleAsync<PostgresCommandLogBenchmarkRecord?>(
                static row => new PostgresCommandLogBenchmarkRecord(
                    row.GetGuid(0),
                    row.GetString(1),
                    row.GetString(2),
                    row.GetString(3),
                    DateTime.SpecifyKind(row.GetDateTime(4), DateTimeKind.Utc),
                    row.GetString(5)),
                cancellationToken);

    public async Task DeleteAsync(Guid commandId, CancellationToken cancellationToken = default)
        => _ = await Database.Use(CommandLogBenchmarkStatements.DeletePostgres)
            .SetParameters(new CommandIdParameter(commandId))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);

    readonly record struct InsertParameters(CommandLogBenchmarkEntry Entry) : IBindValue
    {
        public object Bind() => Values(
            Uuid(Entry.CommandId),
            Text(Entry.StreamId),
            Text(Entry.ActorName),
            Text(Entry.CommandName),
            Text($"{Entry.CommandTimestampUtc:o}"),
            Text($"{CommandStatus.InProgress}"),
            Text(Entry.JsonCommandData));
    }

    readonly record struct CommandIdParameter(Guid CommandId) : IBindValue
    {
        public object Bind() => Values(Uuid(CommandId));
    }
}
