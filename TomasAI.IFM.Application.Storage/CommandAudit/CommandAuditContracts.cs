using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Storage.CommandAudit;

public sealed record CommandAuditEnvelope(
    Guid CommandId,
    string StreamId,
    string ActorName,
    string CommandName,
    DateTime CommandTimestampUtc,
    CommandAuditPayload Payload)
{
    public int PayloadBytes => Payload.Bytes.Length;

    public static CommandAuditEnvelope Create(ICommand command, CommandAuditMessagePackCodec codec)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.CommandId == Guid.Empty) throw new ArgumentException("Command ID is required.", nameof(command));
        ArgumentException.ThrowIfNullOrWhiteSpace(command.StreamId);
        return new CommandAuditEnvelope(
            command.CommandId,
            command.StreamId,
            command.RouteTo.ToString(),
            command.CommandName,
            DateTime.UtcNow,
            codec.Serialize(command));
    }
}

internal readonly record struct CommandAuditWriteResult(bool Accepted, bool LegacyConflict);

internal interface ICommandAuditWriter : IAsyncDisposable
{
    ValueTask<CommandAuditWriteResult> ReserveAsync(
        CommandAuditEnvelope envelope,
        CancellationToken cancellationToken = default);
}

public sealed class CommandAuditPayloadConflictException(Guid commandId)
    : InvalidOperationException($"Command ID {commandId} is already associated with a different command payload.")
{
    public Guid CommandId { get; } = commandId;
}

public sealed class CommandAuditDuplicateException(Guid commandId)
    : InvalidOperationException($"Command ID {commandId} already has a durable audit reservation.")
{
    public Guid CommandId { get; } = commandId;
}
