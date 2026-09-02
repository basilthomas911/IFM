using System.Collections.Concurrent;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

public enum MarketOutlookComponentType : byte
{
    Rsi,
    Tdi,
    ItiLatest,
    ItiDirection,
    ItiExtreme,
    ItiReversal,
    Vx,
    Eod,
    Ema,
    BollingerBand,
    EsTrade,
    TradeSignal,
    FeedHealth
}

/// <summary>
/// Describes where a latest-arrival cache value came from. These values are diagnostic only and
/// never participate in cache admission or ordering decisions.
/// </summary>
public readonly record struct MarketOutlookSourcePosition(
    Guid SourceId,
    long SourceSequence,
    DateTime SourceTimestampUtc,
    Guid StreamEpochId = default,
    long StreamOrdinal = 0);

/// <summary>One component position written in the same atomic partial-state transaction.</summary>
public readonly record struct MarketOutlookComponentWrite(
    MarketOutlookComponentType Component,
    MarketOutlookSourcePosition Position);

public sealed record MarketOutlookInputState
{
    public MarketOutlookEntityId EntityId { get; init; } = new();
    public FuturesEodDataV2ReadModel? FuturesEodData { get; init; }
    public FuturesTradeSignalV2ReadModel? FuturesTradeSignal { get; init; }
    public FuturesRsiSignalReadModel? FuturesRsiSignal { get; init; }
    public FuturesTdiSignalReadModel? FuturesTdiSignal { get; init; }
    public FuturesItiSignalV2ReadModel? TrendDirectionChange { get; init; }
    public FuturesItiSignalV2ReadModel? TrendExtremeChange { get; init; }
    public FuturesItiSignalV2ReadModel? TrendReversalChange { get; init; }
    public FuturesItiSignalV2ReadModel? LatestItiTrendSignal { get; init; }
    public decimal? VixFuturesSessionOpenPrice { get; init; }
    public decimal? VixFuturesPrice { get; init; }
    public FuturesEmaSignalReadModel? FuturesEmaSignal { get; init; }
    public FuturesBbSignalReadModel? FuturesBbSignal { get; init; }
    public decimal? CurrentEsPrice { get; init; }
    public DateTime MarketDataAsOfUtc { get; init; }
    public string FeedHealth { get; init; } = "Unavailable";
    public string FeedHealthReason { get; init; } = string.Empty;
    public ImmutableDictionary<MarketOutlookComponentType, MarketOutlookSourcePosition> Positions { get; init; }
        = ImmutableDictionary<MarketOutlookComponentType, MarketOutlookSourcePosition>.Empty;
}

public readonly record struct MarketOutlookHotCacheWriteResult(
    MarketOutlookInputState Inputs,
    MarketOutlookReadModel Snapshot);

public readonly record struct MarketOutlookHotCacheMetrics(
    long ReceivedInputUpdates,
    long WrittenInputUpdates,
    long ComposedSnapshots,
    long Queries,
    long NotificationFailures,
    long CompositionFailures,
    DateTime? LastComponentRefreshUtc,
    DateTime? LastEsRefreshUtc);

public interface IMarketOutlookHotCache
{
    bool TryGetInputs(MarketOutlookEntityId entityId, out MarketOutlookInputState state);
    bool TryGetCurrent(MarketOutlookEntityId entityId, out MarketOutlookReadModel value);
    MarketOutlookHotCacheMetrics GetMetrics();
    void Clear();
}

/// <summary>Mutation capability reserved for the single Market Outlook update processor.</summary>
public interface IMarketOutlookHotCacheWriter
{
    MarketOutlookHotCacheWriteResult Write(
        MarketOutlookEntityId entityId,
        IReadOnlyCollection<MarketOutlookComponentWrite> components,
        Func<MarketOutlookInputState, MarketOutlookInputState> update,
        Func<MarketOutlookInputState, MarketOutlookReadModel> compose);
    void RecordNotificationFailure();
}

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
