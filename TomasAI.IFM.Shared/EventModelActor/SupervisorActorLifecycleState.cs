using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

public enum SupervisorActorLifecycleState
{
    Registered = 0,
    Starting = 1,
    Running = 2,
    Draining = 3,
    Stopped = 4,
    Restarting = 5,
    Faulted = 6,
    TimedOut = 7,
    Quarantined = 8
}
