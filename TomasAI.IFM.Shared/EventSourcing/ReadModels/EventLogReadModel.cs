namespace TomasAI.IFM.Shared.EventSourcing.ViewModels;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized/configuration array contract and consumer bindings; changing collection types is a separate API migration.")]
public record EventLogReadModel(
    long EventStreamId,
    string EventName,
    string EventTypeName,
    long EventVersion,
    byte[] EventData,
    Guid CommandId,
    string EventTimestamp,
    long StreamVersion = 0)
{
    public IEvent ToDomainEvent() => EventLogBinaryReader.Read(EventTypeName, EventVersion, EventData);
    public IEvent ToDomainEvent(IEventTypeResolver resolver) => EventLogBinaryReader.Read(EventTypeName, EventVersion, EventData, resolver);
}
