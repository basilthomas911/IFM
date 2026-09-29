using System.Collections.Frozen;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Configures exact actor message routes whose routine Information entry and successful-exit logs are suppressed.</summary>
public sealed class ActorInformationLoggingOptions
{
    public const string SectionName = "ActorRuntime:InformationLogging";
    public ActorType[] SuppressedActorTypes { get; set; } = [];
    public ActorInformationLogSuppressionRoute[] SuppressedRoutes { get; set; } = [];

    public ActorInformationLoggingPolicy Compile()
    {
        var configuredActorTypes = SuppressedActorTypes.Length == 0
            ? new[] { ActorType.Event, ActorType.Realtime }
            : SuppressedActorTypes;
        var actorTypes = new HashSet<ActorType>();
        foreach (var actorType in configuredActorTypes)
        {
            if (!Enum.IsDefined(actorType))
                throw new InvalidOperationException($"Unknown suppressed actor type '{actorType}' in {SectionName}.");
            if (!actorTypes.Add(actorType))
                throw new InvalidOperationException($"Duplicate suppressed actor type '{actorType}' in {SectionName}.");
        }
        var routes = new HashSet<ActorInformationLogRoute>();
        foreach (var route in SuppressedRoutes)
        {
            ArgumentNullException.ThrowIfNull(route);
            if (!Enum.IsDefined(route.ActorType))
                throw new InvalidOperationException($"Unknown actor type '{route.ActorType}' in {SectionName}.");
            if (string.IsNullOrWhiteSpace(route.Name))
                throw new InvalidOperationException($"Actor name is required in {SectionName}.");
            if (string.IsNullOrWhiteSpace(route.Verb))
                throw new InvalidOperationException($"Actor verb is required in {SectionName}.");
            var key = new ActorInformationLogRoute(route.ActorType, route.Name, route.Verb);
            if (!routes.Add(key))
                throw new InvalidOperationException($"Duplicate actor information-log suppression route '{route.ActorType}.{route.Name}.{route.Verb}'.");
        }
        return new ActorInformationLoggingPolicy(actorTypes.ToFrozenSet(), routes.ToFrozenSet());
    }
}

/// <summary>Identifies one exact actor type, actor name, and message verb whose routine Information logs are suppressed.</summary>
public sealed class ActorInformationLogSuppressionRoute
{
    public ActorType ActorType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Verb { get; set; } = string.Empty;
}

readonly record struct ActorInformationLogRoute(ActorType ActorType, string Name, string Verb);

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
