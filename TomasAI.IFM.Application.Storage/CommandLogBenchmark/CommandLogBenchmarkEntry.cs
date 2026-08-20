using Newtonsoft.Json;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Storage.CommandLogBenchmark;

/// <summary>
/// Immutable input shared by the isolated PostgreSQL/ScyllaDB command-log benchmark.
/// PostgreSQL receives the current JSON representation; ScyllaDB receives MessagePack bytes.
/// </summary>
public sealed record CommandLogBenchmarkEntry(
    Guid CommandId,
    string StreamId,
    string ActorName,
    string CommandName,
    DateTime CommandTimestampUtc,
    string JsonCommandData,
    byte[] MessagePackCommandData)
{
    /// <summary>Creates both payload representations once, outside the measured database operation.</summary>
    public static CommandLogBenchmarkEntry Create<TCommand>(
        Guid commandId,
        string streamId,
        string actorName,
        string commandName,
        DateTime commandTimestampUtc,
        TCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);

        var messagePack = new MessagePackBinarySerializer().Serialize(command)
            ?? throw new InvalidOperationException("MessagePack command serialization returned no data.");
        return new CommandLogBenchmarkEntry(
            commandId,
            streamId,
            actorName,
            commandName,
            commandTimestampUtc.Kind == DateTimeKind.Utc
                ? commandTimestampUtc
                : commandTimestampUtc.ToUniversalTime(),
            JsonConvert.SerializeObject(command),
            messagePack);
    }
}

/// <summary>
/// Deliberately narrow comparison contract. It is not registered in application runtime dependency injection.
/// </summary>
public interface ICommandLogBenchmarkStore
{
    Task<bool> TryInsertAsync(
        CommandLogBenchmarkEntry entry,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid commandId, CancellationToken cancellationToken = default);
}

public sealed record ScyllaCommandLogBenchmarkRecord(
    Guid CommandId,
    string StreamId,
    string ActorName,
    string CommandName,
    DateTime CommandTimestampUtc,
    byte[] MessagePackCommandData);

public sealed record PostgresCommandLogBenchmarkRecord(
    Guid CommandId,
    string StreamId,
    string ActorName,
    string CommandName,
    DateTime CommandTimestampUtc,
    string JsonCommandData);
