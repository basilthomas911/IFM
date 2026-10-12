



using TomasAI.IFM.Shared.EventSourcing;


namespace TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;

internal sealed record PreparedEventLogEntry(
    int EventNameId,
    IEvent DomainEvent,
    byte[] Payload,
    DurableProjectionRequirement? RequiredProjection);
