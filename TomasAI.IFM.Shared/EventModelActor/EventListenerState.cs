using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Specifies the operational state of an event listener.
/// </summary>
/// <remarks>Use this enumeration to determine whether an event listener is currently running, stopped, or in an
/// unknown state. The state can be used to control event processing or to report listener status in monitoring
/// scenarios.</remarks>
public enum EventListenerState
{
    Unknown,
    Started,
    Running,
    Stopped
}

public static class EventListenerStateExtensions
{
    public static string ToStringFast(this EventListenerState value) => value switch
    {
        EventListenerState.Unknown => nameof(EventListenerState.Unknown),
        EventListenerState.Started => nameof(EventListenerState.Started),
        EventListenerState.Running => nameof(EventListenerState.Running),
        EventListenerState.Stopped => nameof(EventListenerState.Stopped),
        _ => value.ToString()
    };
}
