namespace TomasAI.IFM.Shared.EventSourcing.ViewModels;

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
}

public class EventStreamReadModel
{
    public long EventVersion { get; set; }
    public long StreamVersion { get; set; }
    public string EventTypeName { get; set; } = string.Empty;
    public byte[] EventData { get; set; } = [];
    public IEvent ToDomainEvent() => EventLogBinaryReader.Read(EventTypeName, EventVersion, EventData);
}

internal static class EventLogBinaryReader
{
    internal static IEvent Read(string typeName, long version, byte[] payload)
    {
        // An unavailable event type remains observable. Corrupt known events must fail replay.
        if (Type.GetType(typeName, false, true) is null)
            return new UnknownEvent(subject: default, id: Guid.Empty, entityId: default,
                eventId: version, commandId: Guid.Empty, aggregateId: string.Empty,
                eventSource: string.Empty, receivedOn: DateTime.MinValue, eventSourceId: 0,
                eventSourceVersion: 0, eventTypeName: typeName,
                eventData: Convert.ToBase64String(payload), eventDate: DateTime.MinValue);
        return EventLogMessagePackCodec.Shared.Deserialize(typeName, version, payload);
    }
}
