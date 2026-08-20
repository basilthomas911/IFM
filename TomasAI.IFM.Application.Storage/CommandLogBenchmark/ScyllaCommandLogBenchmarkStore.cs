using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.CommandLogBenchmark;

/// <summary>
/// MessagePack/blob ScyllaDB candidate used only by the comparison harness. LWT makes command-id insertion atomic.
/// </summary>
public sealed class ScyllaCommandLogBenchmarkStore(
    IDbConnectionSettings connectionSettings,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<ScyllaCommandLogBenchmarkStore>(
        connectionSettings[ConnectionName],
        logger),
      ICommandLogBenchmarkStore
{
    public const string ConnectionName = "CommandLogScyllaBenchmarkConnection";

    public override IObjectRepository Database => this;

    public Task CreateSchemaAsync(CancellationToken cancellationToken = default)
        => Database.Use(CommandLogBenchmarkStatements.CreateScyllaTable)
            .ExecuteCommandAsync(cancellationToken);

    public async Task<bool> TryInsertAsync(
        CommandLogBenchmarkEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return await Database.Use(CommandLogBenchmarkStatements.TryInsertScylla)
            .SetParameters(new InsertParameters(entry))
            .ExecuteSingleAsync(static row => row.GetBool(0), cancellationToken)
            .ConfigureAwait(false) == true;
    }

    public Task<ScyllaCommandLogBenchmarkRecord?> GetAsync(
        Guid commandId,
        CancellationToken cancellationToken = default)
        => Database.Use(CommandLogBenchmarkStatements.GetScylla)
            .SetParameters(new CommandIdParameter(commandId))
            .ExecuteSingleAsync<ScyllaCommandLogBenchmarkRecord?>(
                static row => new ScyllaCommandLogBenchmarkRecord(
                    row.GetGuid(0),
                    row.GetString(1),
                    row.GetString(2),
                    row.GetString(3),
                    DateTime.SpecifyKind(row.GetDateTime(4), DateTimeKind.Utc),
                    row.GetBytes(5)),
                cancellationToken);

    public async Task DeleteAsync(Guid commandId, CancellationToken cancellationToken = default)
        => _ = await Database.Use(CommandLogBenchmarkStatements.DeleteScylla)
            .SetParameters(new CommandIdParameter(commandId))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);

    readonly record struct InsertParameters(CommandLogBenchmarkEntry Entry) : IBindValue
    {
        public object Bind() => new object?[]
        {
            Entry.CommandId,
            Entry.StreamId,
            Entry.ActorName,
            Entry.CommandName,
            Entry.CommandTimestampUtc,
            Entry.MessagePackCommandData
        };
    }

    readonly record struct CommandIdParameter(Guid CommandId) : IBindValue
    {
        public object Bind() => new object?[] { CommandId };
    }
}
