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
}
