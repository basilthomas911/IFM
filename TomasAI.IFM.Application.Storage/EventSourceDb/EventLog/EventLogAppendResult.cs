


namespace TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;

public sealed record EventLogAppendResult(IReadOnlyList<EventLogAssignment> Assignments, DateTime CommittedAtUtc);
