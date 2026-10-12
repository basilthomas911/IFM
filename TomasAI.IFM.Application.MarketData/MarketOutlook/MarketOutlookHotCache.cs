using System.Collections.Concurrent;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

/// <summary>
/// Process-local latest-arrival Market Outlook cache. Its mutation capability is held by one
/// processor; readers atomically capture immutable references without taking an application lock.
/// </summary>
public sealed class MarketOutlookHotCache : IMarketOutlookHotCache, IMarketOutlookHotCacheWriter
{
    sealed record PublishedState(MarketOutlookInputState Inputs, MarketOutlookReadModel? Current);

    sealed class Cell(MarketOutlookEntityId id)
    {
        public PublishedState State = new(new() { EntityId = id }, null);
    }

    readonly ConcurrentDictionary<MarketOutlookEntityId, Cell> cells = new();
    long received;
    long written;
    long composed;
    long queries;
    long notificationFailures;
    long compositionFailures;
    long lastComponentTicks;
    long lastEsTicks;

    public static MarketOutlookHotCache Shared { get; } = new();

    public MarketOutlookHotCacheWriteResult Write(
        MarketOutlookEntityId entityId,
        IReadOnlyCollection<MarketOutlookComponentWrite> components,
        Func<MarketOutlookInputState, MarketOutlookInputState> update,
        Func<MarketOutlookInputState, MarketOutlookReadModel> compose)
    {
        using var mutation = TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.EnterMutation();
        ArgumentNullException.ThrowIfNull(entityId);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(compose);
        Interlocked.Add(ref received, components.Count);

        var cell = cells.GetOrAdd(entityId, static id => new Cell(id));
        var previous = Volatile.Read(ref cell.State).Inputs;
        var positions = previous.Positions;
        var marketDataAsOfUtc = previous.MarketDataAsOfUtc;
        foreach (var component in components)
        {
            positions = positions.SetItem(component.Component, component.Position);
            if (component.Position.SourceTimestampUtc > marketDataAsOfUtc)
                marketDataAsOfUtc = component.Position.SourceTimestampUtc;
        }

        var next = update(previous) with
        {
            EntityId = entityId,
            MarketDataAsOfUtc = marketDataAsOfUtc,
            Positions = positions
        };
        MarketOutlookReadModel snapshot;
        try
        {
            snapshot = compose(next);
        }
        catch
        {
            Interlocked.Increment(ref compositionFailures);
            throw;
        }
        Volatile.Write(ref cell.State, new(next, snapshot));

        Interlocked.Add(ref written, components.Count);
        Interlocked.Increment(ref composed);
        var nowTicks = DateTime.UtcNow.Ticks;
        if (components.Any(static value => value.Component == MarketOutlookComponentType.EsTrade))
            Interlocked.Exchange(ref lastEsTicks, nowTicks);
        else
            Interlocked.Exchange(ref lastComponentTicks, nowTicks);
        return new(next, snapshot);
    }

    public bool TryGetInputs(MarketOutlookEntityId entityId, out MarketOutlookInputState state)
    {
        if (!cells.TryGetValue(entityId, out var cell))
        {
            state = default!;
            return false;
        }
        state = Volatile.Read(ref cell.State).Inputs;
        return true;
    }

    public bool TryGetCurrent(MarketOutlookEntityId entityId, out MarketOutlookReadModel value)
    {
        Interlocked.Increment(ref queries);
        if (!cells.TryGetValue(entityId, out var cell)
            || Volatile.Read(ref cell.State).Current is not { } current)
        {
            value = default!;
            return false;
        }
        value = current;
        return true;
    }

    public void RecordNotificationFailure() => Interlocked.Increment(ref notificationFailures);

    public MarketOutlookHotCacheMetrics GetMetrics() => new(
        Interlocked.Read(ref received),
        Interlocked.Read(ref written),
        Interlocked.Read(ref composed),
        Interlocked.Read(ref queries),
        Interlocked.Read(ref notificationFailures),
        Interlocked.Read(ref compositionFailures),
        ReadTime(ref lastComponentTicks),
        ReadTime(ref lastEsTicks));

    public void Clear() => cells.Clear();

    static DateTime? ReadTime(ref long ticks)
    {
        var value = Interlocked.Read(ref ticks);
        return value == 0 ? null : new DateTime(value, DateTimeKind.Utc);
    }
}
