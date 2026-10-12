using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

public sealed record CommandAuditEnvelope(
    Guid CommandId,
    string StreamId,
    string ActorName,
    string CommandName,
    DateTime CommandTimestampUtc,
    CommandAuditPayload Payload)
{
    public int PayloadBytes => Payload.Bytes.Length;
    internal Type? RetryCommandType { get; init; }
    internal byte[] RetrySha256 { get; init; } = Payload.Sha256;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "The validated member belongs to this public input argument; explicitly report its root parameter rather than exposing a member expression as the parameter name.")]
    public static CommandAuditEnvelope Create(ICommand command, CommandAuditMessagePackCodec codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(command);
        if (command.CommandId == Guid.Empty) throw new ArgumentException("Command ID is required.", nameof(command));
        ArgumentException.ThrowIfNullOrWhiteSpace(command.StreamId, nameof(command));
        var payload = codec.Serialize(command);
        return new CommandAuditEnvelope(
            command.CommandId,
            command.StreamId,
            command.RouteTo.ToString(),
            command.CommandName,
            DateTime.UtcNow,
            payload)
        {
            RetryCommandType = command is ICommandRetryIdentity ? command.GetType() : null,
            RetrySha256 = command is ICommandRetryIdentity retry ? codec.Serialize(retry.ForRetryIdentity()).Sha256 : payload.Sha256
        };
    }
}
