using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.EventSourcing.ViewModels;

public record EventLogReadModel(
    long EventStreamId,
    string EventName,
    string EventTypeName,   
    long EventVersion,
    string EventData,
    Guid CommandId,
    string EventTimestamp)
{
    /// <summary>
    /// deserialize event data from event type
    /// </summary>
    /// <returns></returns>
    public IEvent ToDomainEvent()
    {
        IEvent? domainEvent = default;
        if (!string.IsNullOrEmpty(EventTypeName))
        {
            var domainEventType = Type.GetType(EventTypeName, true,true);
            if (domainEventType is not null && !string.IsNullOrEmpty(EventData))
            {
                try
                {
                    domainEvent = JsonConvert.DeserializeObject(EventData, domainEventType) as IEvent;
                    if (domainEvent is not null)
                        EventModelActor.EventInitHelper.SetProperty(domainEvent, nameof(IEvent.EventId), EventVersion);
                }
                catch { }
            }
        }
        return domainEvent is null
            ? ToUnknownEvent()
            : domainEvent;
    }

    internal IEvent ToUnknownEvent()
       => new UnknownEvent(
           subject: default,
           id: Guid.Empty,
           entityId: default,
           eventId: EventVersion,
           commandId: Guid.Empty,
           aggregateId: string.Empty,
           eventSource: string.Empty,
           receivedOn: DateTime.MinValue,
           eventSourceId: 0L,
           eventSourceVersion: 0L,
           eventTypeName: EventTypeName,
           eventData: EventData,
           eventDate: DateTime.MinValue);
}

public class EventStreamReadModel
{
    public long EventVersion { get; set; }
    public string EventTypeName { get; set; }
    public string EventData { get; set; }

    /// <summary>
    /// Converts the current <see cref="EventStreamReadModel"/> to a domain event.
    /// </summary>
    /// <returns>A new instance of a domain event.</returns>
    public IEvent ToDomainEvent()
    {
        IEvent? domainEvent = default;
        if (!string.IsNullOrEmpty(EventTypeName))
        {
            var domainEventType = Type.GetType(EventTypeName, false, true);
            if (domainEventType is not null && !string.IsNullOrEmpty(EventData))
            {
                try
                {
                    domainEvent = JsonConvert.DeserializeObject(EventData, domainEventType) as IEvent;
                    if (domainEvent is not null)
                        EventModelActor.EventInitHelper.SetProperty(domainEvent, nameof(IEvent.EventId), EventVersion);
                }
                catch { }
            }
        }
        return domainEvent is null
            ? ToUnknownEvent()
            : domainEvent;

          IEvent ToUnknownEvent()
            => new UnknownEvent(
               subject: default,
               id: Guid.Empty,
               entityId: default,
               eventId: EventVersion,
               commandId: Guid.Empty,
               aggregateId: string.Empty,
               eventSource: string.Empty,
               receivedOn: DateTime.MinValue,
               eventSourceId: 0L,
               eventSourceVersion: 0L,
               eventTypeName: EventTypeName,
               eventData: EventData,
               eventDate: DateTime.MinValue);
    }
}
