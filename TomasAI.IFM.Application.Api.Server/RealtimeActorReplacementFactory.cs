using System.Reflection;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>Constructs fresh actor-owned realtime dependencies while retaining shared infrastructure.</summary>
internal static class RealtimeActorReplacementFactory
{
    /// <summary>Creates a new actor and context, bypassing singleton actor registrations.</summary>
    internal static IActor Create(Type actorType, Func<Type, object> resolve)
        => (IActor)Construct(actorType, resolve);

    /// <summary>Invokes the implementation constructor with fresh actor contexts and shared infrastructure.</summary>
    static object Construct(Type implementation, Func<Type, object> resolve)
    {
        var constructor = implementation.GetConstructors().OrderByDescending(candidate => candidate.GetParameters().Length).First();
        var arguments = constructor.GetParameters().Select(parameter => Resolve(parameter, resolve)).ToArray();
        return constructor.Invoke(arguments);
    }

    /// <summary>Resolves one dependency, replacing actor-owned context registrations while honoring optional dependencies.</summary>
    static object? Resolve(ParameterInfo parameter, Func<Type, object> resolve)
    {
        object? dependency;
        try { dependency = resolve(parameter.ParameterType); }
        catch when (parameter.HasDefaultValue) { return parameter.DefaultValue; }
        if (dependency is null)
        {
            if (parameter.HasDefaultValue) return parameter.DefaultValue;
            throw new InvalidOperationException($"Replacement dependency '{parameter.ParameterType}' is unavailable.");
        }
        var owned = dependency is IEventActorContext context && context.ActorId.ActorType == TomasAI.IFM.Shared.EventModelActor.ActorType.Realtime;
        return owned ? Construct(dependency.GetType(), resolve) : dependency;
    }
}
