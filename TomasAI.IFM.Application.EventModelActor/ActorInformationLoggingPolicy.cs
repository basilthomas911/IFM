using System.Collections.Frozen;

using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.EventModelActor;

/// <summary>Immutable, allocation-free lookup policy compiled before any actor is constructed.</summary>
public sealed class ActorInformationLoggingPolicy
{
    readonly FrozenSet<ActorType> _suppressedActorTypes;
    readonly FrozenSet<ActorInformationLogRoute> _suppressedRoutes;
    internal ActorInformationLoggingPolicy(
        FrozenSet<ActorType> suppressedActorTypes,
        FrozenSet<ActorInformationLogRoute> suppressedRoutes)
    {
        _suppressedActorTypes = suppressedActorTypes;
        _suppressedRoutes = suppressedRoutes;
    }
    public static ActorInformationLoggingPolicy Default { get; } = new ActorInformationLoggingOptions().Compile();
    public bool SuppressesRoutineInformation(ActorThreadId actorThreadId, string verb)
        => _suppressedActorTypes.Contains(actorThreadId.ActorType)
           || _suppressedRoutes.Contains(new(actorThreadId.ActorType, actorThreadId.Name, verb));
}
