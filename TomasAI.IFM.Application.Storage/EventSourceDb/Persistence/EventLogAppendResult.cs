using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;

public sealed record EventLogAppendResult(IReadOnlyList<EventLogAssignment> Assignments, DateTime CommittedAtUtc);
