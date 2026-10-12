using TomasAI.IFM.Shared.EventSourcing;


namespace TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;

public sealed record EventLogAppendEntry(int EventNameId, IEvent DomainEvent);
