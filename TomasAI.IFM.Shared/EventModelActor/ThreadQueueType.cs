using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.EventModelActor
{
    public enum ThreadQueueType
    {
        Channel,
        RingBuffer,
    }

    public static class ThreadQueueTypeExtensions
    {
        public static string ToStringFast(this ThreadQueueType value) => value switch
        {
            ThreadQueueType.Channel => nameof(ThreadQueueType.Channel),
            ThreadQueueType.RingBuffer => nameof(ThreadQueueType.RingBuffer),
            _ => value.ToString()
        };
    }
}
