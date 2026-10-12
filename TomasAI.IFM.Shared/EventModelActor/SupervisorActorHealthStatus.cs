using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

public enum SupervisorActorHealthStatus
{
    Green = 0,
    Yellow = 1,
    Red = 2
}
