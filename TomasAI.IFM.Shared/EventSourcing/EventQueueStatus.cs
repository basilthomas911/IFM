using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.EventSourcing
{
    public enum EventQueueStatus
    {
        Received,
        Completed,
        Failed
    }

    public static class EventQueueStatusExtensions
    {
        public static string ToStringFast(this EventQueueStatus value) => value switch
        {
            EventQueueStatus.Received => nameof(EventQueueStatus.Received),
            EventQueueStatus.Completed => nameof(EventQueueStatus.Completed),
            EventQueueStatus.Failed => nameof(EventQueueStatus.Failed),
            _ => value.ToString()
        };
    }
}
