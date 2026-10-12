using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

public enum ActorFailureSeverity
{
    Information = 0,
    Warning = 1,
    Error = 2,
    Critical = 3
}
