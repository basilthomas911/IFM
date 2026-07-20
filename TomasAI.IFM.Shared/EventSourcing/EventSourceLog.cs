using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.EventSourcing
{
    public record EventSourceLog(
            string EventId,
            string EventName,
            long EventVersion,
            string EventData,
            DateTime EventDate)
    {

        /// <summary>
        /// deserialize event data from event name
        /// </summary>
        /// <returns></returns>
        public IEvent ToDomainEvent()
        {
            IEvent? domainEvent = null;
            if (!string.IsNullOrWhiteSpace(EventName))
            {
                var domainEventType = Type.GetType(EventName);
                if (domainEventType is not null && !string.IsNullOrWhiteSpace(EventData))
                {
                    try
                    {
                        var des = JsonConvert.DeserializeObject(EventData, domainEventType);
                        domainEvent = des as IEvent;
                        //domainEvent.EventId = eventId;
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
                eventId: 0,
                commandId: Guid.Empty,
                aggregateId: string.Empty,
                eventSource: string.Empty,
                receivedOn: DateTime.MinValue,
                eventSourceId: 0,
                eventSourceVersion: this.EventVersion,
                eventTypeName: this.EventName,
                eventData: this.EventData,
                eventDate: this.EventDate);
        
    }
    
}
