using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Maintains copy-on-write realtime route maps. A source has at most one route per destination mailbox,
/// allowing a registration to replace its scheduling-identity projection without duplicate delivery.
/// Each published map owns its immutable snapshot so reads do not allocate or observe partial updates.
/// </summary>
internal sealed class RealtimeRouteRegistry
{
    sealed class RouteSet(ImmutableDictionary<ActorMailboxId, RealtimeActorRoute> routes)
    {
        internal readonly ImmutableDictionary<ActorMailboxId, RealtimeActorRoute> Routes = routes;
        internal readonly ImmutableArray<RealtimeActorRoute> Snapshot = [.. routes.Values];
    }

    readonly ConcurrentDictionary<ActorTypeId, RouteSet> routes = [];

    internal void Add(ActorTypeId source, RealtimeActorRoute route)
        => routes.AddOrUpdate(
            source,
            _ => new RouteSet(ImmutableDictionary<ActorMailboxId, RealtimeActorRoute>.Empty
                .Add(route.Destination, route)),
            (_, prior) => new RouteSet(prior.Routes.SetItem(route.Destination, route)));

    internal void Remove(ActorTypeId source, ActorMailboxId destination)
    {
        while (routes.TryGetValue(source, out var prior))
        {
            var updated = prior.Routes.Remove(destination);
            if (updated.IsEmpty)
            {
                if (routes.TryRemove(new KeyValuePair<ActorTypeId, RouteSet>(source, prior)))
                    return;
            }
            else if (routes.TryUpdate(source, new RouteSet(updated), prior))
            {
                return;
            }
        }
    }

    internal ImmutableArray<RealtimeActorRoute> GetSnapshot(ActorTypeId source)
        => routes.TryGetValue(source, out var result)
            ? result.Snapshot
            : [];
}
