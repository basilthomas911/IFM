using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

public enum ActorMailboxLifecycleState
{
    Running = 0,
    Draining = 1,
    Paused = 2,
    Quarantined = 3,
    Retired = 4
}
