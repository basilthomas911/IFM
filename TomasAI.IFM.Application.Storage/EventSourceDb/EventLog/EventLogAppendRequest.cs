
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;

public sealed record EventLogAppendRequest(
    string EventStream,
    long EventStreamId,
    Guid CommandId,
    IReadOnlyList<EventLogAppendEntry> Events,
    long? ExpectedStreamVersion,
    DateTime EventTimestampUtc,
    CommandAuditEnvelope? CommandAudit = null);
