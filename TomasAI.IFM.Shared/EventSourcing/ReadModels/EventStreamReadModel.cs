namespace TomasAI.IFM.Shared.EventSourcing.ViewModels;

public class EventStreamReadModel
{
    public long EventVersion { get; set; }
    public long StreamVersion { get; set; }
    public string EventTypeName { get; set; } = string.Empty;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized/configuration array contract and consumer bindings; changing collection types is a separate API migration.")]
    public byte[] EventData { get; set; } = [];
    public IEvent ToDomainEvent() => EventLogBinaryReader.Read(EventTypeName, EventVersion, EventData);
    public IEvent ToDomainEvent(IEventTypeResolver resolver) => EventLogBinaryReader.Read(EventTypeName, EventVersion, EventData, resolver);
}
