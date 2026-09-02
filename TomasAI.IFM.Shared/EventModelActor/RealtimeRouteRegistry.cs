using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Maintains copy-on-write realtime route maps. A source has at most one route per destination mailbox,
/// allowing a registration to replace its scheduling-identity projection without duplicate delivery.
/// </summary>
internal sealed class RealtimeRouteRegistry
{
    readonly ConcurrentDictionary<
        ActorTypeId,
        ImmutableDictionary<ActorMailboxId, RealtimeActorRoute>> routes = [];

    internal void Add(ActorTypeId source, RealtimeActorRoute route)
        => routes.AddOrUpdate(
            source,
            _ => ImmutableDictionary<ActorMailboxId, RealtimeActorRoute>.Empty
                .Add(route.Destination, route),
            (_, destinations) => destinations.SetItem(route.Destination, route));

    internal void Remove(ActorTypeId source, ActorMailboxId destination)
    {
        while (routes.TryGetValue(source, out var destinations))
        {
            var updated = destinations.Remove(destination);
            if (updated.IsEmpty)
            {
                if (routes.TryRemove(new KeyValuePair<
                        ActorTypeId,
                        ImmutableDictionary<ActorMailboxId, RealtimeActorRoute>>(
                        source,
                        destinations)))
                    return;
            }
            else if (routes.TryUpdate(source, updated, destinations))
            {
                return;
            }
        }
    }

    internal ImmutableArray<RealtimeActorRoute> GetSnapshot(ActorTypeId source)
        => routes.TryGetValue(source, out var destinations)
            ? [.. destinations.Values]
            : [];
}
