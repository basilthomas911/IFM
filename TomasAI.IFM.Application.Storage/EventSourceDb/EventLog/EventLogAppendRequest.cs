
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;

public sealed record EventLogAppendRequest(
    EventLogBatchIdentity Identity,
    IReadOnlyList<EventLogAppendEntry> Events,
    DateTime EventTimestampUtc,
    CommandAuditEnvelope? CommandAudit = null)
{
    public EventLogAppendRequest(string eventStream, long eventStreamId, Guid commandId,
        IReadOnlyList<EventLogAppendEntry> events, long? expectedStreamVersion, DateTime eventTimestampUtc,
        CommandAuditEnvelope? commandAudit = null)
        : this(new EventLogBatchIdentity(eventStream, eventStreamId, commandId, expectedStreamVersion),
            events, eventTimestampUtc, commandAudit)
    {
    }

    public string EventStream => Identity.Stream;
    public long EventStreamId => Identity.StreamId;
    public Guid CommandId => Identity.CommandId;
    public long? ExpectedStreamVersion => Identity.ExpectedStreamVersion;
}
