using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;

/// <summary>
/// Reports that PostgreSQL did not provide a definitive commit outcome. Callers must reconcile durable state by
/// command ID and must not blindly retry the append.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified storage exception requires its diagnostic/business identity; preserve the existing constructor contract instead of permitting incomplete instances.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified storage exception requires its diagnostic/business identity; preserve the existing constructor contract instead of permitting incomplete instances.")]
public sealed class EventLogCommitOutcomeUnknownException(string message, Exception innerException)
    : Exception(message, innerException);
