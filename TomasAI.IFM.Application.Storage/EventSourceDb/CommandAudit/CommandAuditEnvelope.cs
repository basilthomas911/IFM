using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

public sealed record CommandAuditEnvelope(
    Guid CommandId,
    string StreamId,
    string ActorName,
    string CommandName,
    DateTime CommandTimestampUtc,
    CommandAuditPayload Payload,
    MessageOrigin? Origin = null)
{
    public int PayloadBytes => Payload.Bytes.Length;
    internal Type? RetryCommandType { get; init; }
    public byte[] RetrySha256 { get; internal init; } = Payload.Sha256;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "The validated member belongs to this public input argument; explicitly report its root parameter rather than exposing a member expression as the parameter name.")]
    public static CommandAuditEnvelope Create(ICommand command, CommandAuditMessagePackCodec codec, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(command);
        if (command.CommandId == Guid.Empty) throw new ArgumentException("Command ID is required.", nameof(command));
        ArgumentException.ThrowIfNullOrWhiteSpace(command.StreamId, nameof(command));
        var origin = MessageOrigins.ForPublication(command, timeProvider ?? TimeProvider.System);
        var payload = codec.Serialize(command);
        return new CommandAuditEnvelope(
            command.CommandId,
            command.StreamId,
            command.RouteTo.ToString(),
            command.CommandName,
            origin?.OriginatedAtUtc.UtcDateTime ?? (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime,
            payload,
            origin)
        {
            RetryCommandType = command is ICommandRetryIdentity ? command.GetType() : null,
            RetrySha256 = command is ICommandRetryIdentity retry ? codec.Serialize(retry.ForRetryIdentity()).Sha256 : payload.Sha256
        };
    }
}
