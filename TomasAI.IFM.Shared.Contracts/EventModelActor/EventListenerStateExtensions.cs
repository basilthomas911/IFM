using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.EventModelActor;

public static class EventListenerStateExtensions
{

    extension(EventListenerState value)
    {
        public string ToStringFast() => value switch
        {
            EventListenerState.Unknown => nameof(EventListenerState.Unknown),
            EventListenerState.Started => nameof(EventListenerState.Started),
            EventListenerState.Running => nameof(EventListenerState.Running),
            EventListenerState.Stopped => nameof(EventListenerState.Stopped),
            _ => value.ToString()
        };
    }
}
