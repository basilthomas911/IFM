using TomasAI.IFM.Shared.EventModelActor.Contracts;
using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor;
namespace TomasAI.IFM.Application.EventModelActor;
/// <summary>Only explicitly registered disposable actors may bypass draining during realtime replacement.</summary>
public static class RealtimeActorResetPolicy
{
    static readonly ConcurrentDictionary<Type, byte> replaceable = new();
    /// <summary>The composition root declares eligibility; durable command actors are never eligible.</summary>
    public static void Register(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.IsAbstract || type.ContainsGenericParameters || !typeof(IActor).IsAssignableFrom(type)
            || !typeof(IReplaceableRealtimeActor).IsAssignableFrom(type))
            throw new ArgumentException("A concrete replaceable actor is required.", nameof(type));
        replaceable.TryAdd(type, 0);
    }
    public static bool CanReplace(Type type, ActorType actorType)
    {
        ArgumentNullException.ThrowIfNull(type);
        return actorType == ActorType.Realtime && replaceable.ContainsKey(type);
    }
}
