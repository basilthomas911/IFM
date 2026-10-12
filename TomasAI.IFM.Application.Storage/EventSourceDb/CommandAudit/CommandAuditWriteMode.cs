namespace TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the established public storage/serialized schema contract and identifiers consumed by callers.")]
public enum CommandAuditWriteMode : byte
{
    SequentialJsonLegacy = 0,
    SequentialMessagePack = 1,
    WindowedMessagePack = 2
}
