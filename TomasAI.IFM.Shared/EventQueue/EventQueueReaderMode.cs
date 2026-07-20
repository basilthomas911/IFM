using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.EventQueue
{
    public enum EventQueueReaderMode
    {
        AllItems,
        LastItem,
        Sync,
        Async
    }

    public static class EventQueueReaderModeExtensions
    {
        public static string ToStringFast(this EventQueueReaderMode value) => value switch
        {
            EventQueueReaderMode.AllItems => nameof(EventQueueReaderMode.AllItems),
            EventQueueReaderMode.LastItem => nameof(EventQueueReaderMode.LastItem),
            EventQueueReaderMode.Sync => nameof(EventQueueReaderMode.Sync),
            EventQueueReaderMode.Async => nameof(EventQueueReaderMode.Async),
            _ => value.ToString()
        };
    }
}
