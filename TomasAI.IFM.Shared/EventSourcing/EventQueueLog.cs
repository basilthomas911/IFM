using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.EventSourcing
{
    public record EventQueueLog(
        int EventQueueId,
        long EventId,
        string EventTypeName,
        EventQueueStatus EventQueueStatus,
        DateTime EventQueueDate,
        string EventFailedMessage)
    {
    }
}
