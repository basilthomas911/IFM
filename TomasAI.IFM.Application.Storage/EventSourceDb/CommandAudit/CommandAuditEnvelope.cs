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

}
