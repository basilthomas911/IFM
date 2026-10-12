using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CommandAuditWriteResult(
    bool Accepted,
    bool LegacyConflict,
    bool PayloadConflict = false);
