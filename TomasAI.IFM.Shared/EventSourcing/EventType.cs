using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.EventSourcing
{
    public enum EventType
    {
        DomainEvent,
        ServiceEvent,
        ErrorEvent,
        CompletedEvent,
        ServiceApiEvent
    }

    public static class EventTypeExtensions
    {
        public static string ToStringFast(this EventType value) => value switch
        {
            EventType.DomainEvent => nameof(EventType.DomainEvent),
            EventType.ServiceEvent => nameof(EventType.ServiceEvent),
            EventType.ErrorEvent => nameof(EventType.ErrorEvent),
            EventType.CompletedEvent => nameof(EventType.CompletedEvent),
            EventType.ServiceApiEvent => nameof(EventType.ServiceApiEvent),
            _ => value.ToString()
        };
    }
}
