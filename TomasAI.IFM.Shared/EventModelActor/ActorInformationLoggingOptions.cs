using System.Collections.Frozen;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Configures exact actor message routes whose routine Information entry and successful-exit logs are suppressed.</summary>
public sealed class ActorInformationLoggingOptions
{
    public const string SectionName = "ActorRuntime:InformationLogging";
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized/configuration array contract and consumer bindings; changing collection types is a separate API migration.")]
    public ActorType[] SuppressedActorTypes { get; set; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized/configuration array contract and consumer bindings; changing collection types is a separate API migration.")]
    public ActorInformationLogSuppressionRoute[] SuppressedRoutes { get; set; } = [];

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "This parameterless configuration validation deliberately identifies the invalid bound configuration property in the exception.")]
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
