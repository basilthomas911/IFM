using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Maintains copy-on-write actor route sets so each message observes one stable,
/// deduplicated destination snapshot while routes are added or removed. Separate
/// instances isolate durable event routes from non-durable realtime routes.
/// </summary>
internal sealed class EventRouteRegistry
{
    readonly ConcurrentDictionary<ActorTypeId, ImmutableHashSet<ActorMailboxId>> _routes = [];

    internal void Add(ActorTypeId source, ActorMailboxId destination)
        => _routes.AddOrUpdate(
            source,
            _ => ImmutableHashSet.Create(destination),
            (_, destinations) => destinations.Add(destination));

    internal void Remove(ActorTypeId source, ActorMailboxId destination)
    {
        while (_routes.TryGetValue(source, out var destinations))
        {
            var updated = destinations.Remove(destination);
            if (updated.IsEmpty)
            {
                if (_routes.TryRemove(
                    new KeyValuePair<ActorTypeId, ImmutableHashSet<ActorMailboxId>>(
                        source,
                        destinations)))
                    return;
            }
            else if (_routes.TryUpdate(source, updated, destinations))
            {
                return;
            }
        }
    }

    internal ImmutableHashSet<ActorMailboxId> GetSnapshot(ActorTypeId source)
        => _routes.TryGetValue(source, out var destinations)
            ? destinations
            : ImmutableHashSet<ActorMailboxId>.Empty;
}
