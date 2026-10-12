

namespace TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified storage exception requires its diagnostic/business identity; preserve the existing constructor contract instead of permitting incomplete instances.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified storage exception requires its diagnostic/business identity; preserve the existing constructor contract instead of permitting incomplete instances.")]
public sealed class CommandAuditDuplicateException(Guid commandId)
    : InvalidOperationException($"Command ID {commandId} already has a durable audit reservation.")
{
    public Guid CommandId { get; } = commandId;
}
