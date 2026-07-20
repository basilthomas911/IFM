using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.EventSourcing
{
    public class EventLog
    {
        public long EventId { get; }
        public long EventSourceId { get; }
        public long EventSourceVersion { get; }
        public string EventTypeName { get; }
        public string EventData { get; }
        public DateTime EventDate { get; }
        
        public EventLog(
            long eventId,
            long eventSourceId,
            long eventSourceVersion,
            string eventTypeName,
            string eventData,
            DateTime EventDate)
        {
            this.EventId = eventId;
            this.EventSourceId = eventSourceId;
            this.EventSourceVersion = eventSourceVersion;
            this.EventTypeName = eventTypeName;
            this.EventData = eventData;
            this.EventDate = EventDate;
        }

        /// <summary>
        /// deserialize event data from event type
        /// </summary>
        /// <returns></returns>
        public IEvent ToDomainEvent(long eventId)
        {
            IEvent? domainEvent = null;
            if (!string.IsNullOrWhiteSpace(EventTypeName))
            {
                var domainEventType = Type.GetType(EventTypeName);
                if (domainEventType != null && !string.IsNullOrWhiteSpace(EventData))
                {
                    try
                    {
                        var des = JsonConvert.DeserializeObject(EventData, domainEventType);
                        domainEvent = des as IEvent;
                        if (domainEvent != null)
                        {
                            EventInitHelper.SetProperty(domainEvent, nameof(IEvent.EventId), eventId);
                        }
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
                eventId: this.EventId,
                commandId: Guid.Empty,
                aggregateId: string.Empty,
                eventSource: string.Empty,
                receivedOn: DateTime.MinValue,
                eventSourceId: this.EventSourceId,
                eventSourceVersion: this.EventSourceVersion,
                eventTypeName: this.EventTypeName,
                eventData: this.EventData,
                eventDate: this.EventDate);
        
    }
    
}
