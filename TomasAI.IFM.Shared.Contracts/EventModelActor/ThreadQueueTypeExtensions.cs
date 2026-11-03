using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.EventModelActor
{

    public static class ThreadQueueTypeExtensions
    {

        extension(ThreadQueueType value)
        {
            public string ToStringFast() => value switch
            {
                ThreadQueueType.Channel => nameof(ThreadQueueType.Channel),
                ThreadQueueType.RingBuffer => nameof(ThreadQueueType.RingBuffer),
                _ => value.ToString()
            };
        }
    }
}
