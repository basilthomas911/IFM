




using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;

internal sealed record PreparedEventLogRequest(
    string EventStream,
    long EventStreamId,
    Guid CommandId,
    IReadOnlyList<PreparedEventLogEntry> Events,
    long? ExpectedStreamVersion,
    DateTime EventTimestampUtc,
    int PayloadBytes,
    CommandAuditEnvelope? CommandAudit);
