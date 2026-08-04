using System.Collections.Immutable;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

internal static class EventFanoutRoutes
{
    internal static IReadOnlyList<ActorSubject> Build(
        ActorSubject source,
        ImmutableHashSet<ActorMailboxId> routes,
        bool includePrimary)
    {
        var destinations = new List<ActorSubject>(routes.Count + (includePrimary ? 1 : 0));
        if (includePrimary)
            destinations.Add(source);
        foreach (var route in routes)
        {
            if (includePrimary && route == source.ActorId)
                continue;
            destinations.Add(new ActorSubject(
                route.ActorType,
                route.Name,
                source.Verb,
                source.EntityId));
        }
        return destinations;
    }
}
