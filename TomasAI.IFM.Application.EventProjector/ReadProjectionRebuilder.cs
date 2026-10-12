using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventProjector;

/// <summary>Controlled reconstruction from persisted events using explicitly registered materialization-only actions.</summary>
/// <remarks>Does not reset tables, publish events, enqueue commands, or execute live projector actions.</remarks>
public sealed class ReadProjectionRebuilder(IReadOnlyCollection<EventProjectionDescriptor> descriptors)
{
    readonly Dictionary<Type, EventProjectionDescriptor[]> handlers = Validate(descriptors);

    /// <summary>Applies a source event log in ascending persisted source order. The caller supplies selected persisted streams after quiescing writes; sequence allocation alone is not a live commit cursor.</summary>
    public async Task<long> RebuildAsync(IAsyncEnumerable<IEvent> sources, CancellationToken token = default)
    {
        long count = 0, previous = 0;
        await foreach (var source in sources.WithCancellation(token).ConfigureAwait(false))
        {
            if (source.EventId <= previous)
                throw new InvalidDataException("Reconstruction requires committed source events in strictly ascending event-log order.");
            if (!handlers.TryGetValue(source.GetType(), out var actions))
                throw new InvalidOperationException($"No materialization-only rebuild is registered for {source.EventName}.");
            foreach (var action in actions)
                await action.RebuildReadModelAsync!(source, token).ConfigureAwait(false);
            previous = source.EventId;
            count++;
        }
        return count;
    }

    static Dictionary<Type, EventProjectionDescriptor[]> Validate(IReadOnlyCollection<EventProjectionDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        if (descriptors.FirstOrDefault(descriptor => descriptor.RebuildReadModelAsync is null) is { } missing)
            throw new InvalidOperationException($"{missing.SourceEventType.Name} has no audited read-model rebuild action. Live actions must not be used as a fallback.");
        return descriptors.GroupBy(x => x.SourceEventType).ToDictionary(x => x.Key, x => x.ToArray());
    }
}
