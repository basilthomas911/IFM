using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.EventQueue
{

    public static class EventQueueReaderModeExtensions
    {

        extension(EventQueueReaderMode value)
        {
            public string ToStringFast() => value switch
            {
                EventQueueReaderMode.AllItems => nameof(EventQueueReaderMode.AllItems),
                EventQueueReaderMode.LastItem => nameof(EventQueueReaderMode.LastItem),
                EventQueueReaderMode.Sync => nameof(EventQueueReaderMode.Sync),
                EventQueueReaderMode.Async => nameof(EventQueueReaderMode.Async),
                _ => value.ToString()
            };
        }
    }
}
