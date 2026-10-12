





namespace TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the established public storage/serialized schema contract and identifiers consumed by callers.")]
public sealed record CommandAuditPayload(
    CommandAuditPayloadFormat Format,
    int Version,
    byte[] Bytes,
    byte[] Sha256);
