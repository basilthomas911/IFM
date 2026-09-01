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
    TradeSignal
}

public readonly record struct MarketOutlookSourcePosition(
    Guid SourceId,
    long SourceSequence,
    DateTime SourceTimestampUtc,
    Guid StreamEpochId = default,
    long StreamOrdinal = 0)
{
    public bool IsNewerThan(MarketOutlookSourcePosition current)
    {
        if (SourceId != Guid.Empty && SourceId == current.SourceId)
            return false;
        if (StreamEpochId != Guid.Empty && current.StreamEpochId != Guid.Empty)
            return StreamEpochId == current.StreamEpochId
                && StreamOrdinal > 0 && current.StreamOrdinal > 0
                ? StreamOrdinal > current.StreamOrdinal
                : SourceTimestampUtc > current.SourceTimestampUtc;
        if (SourceSequence > 0 && current.SourceSequence > 0)
            return SourceSequence > current.SourceSequence;
        return SourceTimestampUtc > current.SourceTimestampUtc;
    }
}

public readonly record struct MarketOutlookGenerationFence(
    string ContractId,
    DateOnly ValueDate,
    Guid GenerationId = default)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(ContractId) && ValueDate != default;
    public bool Accepts(MarketOutlookEntityId id) => IsValid
        && string.Equals(ContractId, id.ContractId, StringComparison.Ordinal)
        && ValueDate == id.ValueDate;
}

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
    public decimal? VixFuturesPrice { get; init; }
    public FuturesEmaSignalReadModel? FuturesEmaSignal { get; init; }
    public FuturesBbSignalReadModel? FuturesBbSignal { get; init; }
    public decimal? CurrentEsPrice { get; init; }
    public DateTime MarketDataAsOfUtc { get; init; }
    public ImmutableDictionary<MarketOutlookComponentType, MarketOutlookSourcePosition> Positions { get; init; }
        = ImmutableDictionary<MarketOutlookComponentType, MarketOutlookSourcePosition>.Empty;
}

public readonly record struct MarketOutlookHotCacheMetrics(
    long AcceptedInputUpdates,
    long RejectedInputUpdates,
    long ProjectionUpdates,
    long Queries,
    DateTime? LastComponentRefreshUtc,
    DateTime? LastEsRefreshUtc);

public interface IMarketOutlookHotCache
{
    MarketOutlookGenerationFence ActiveFence { get; }
    void Activate(MarketOutlookGenerationFence fence);
    bool TryUpdateInput(
        MarketOutlookEntityId entityId,
        MarketOutlookComponentType component,
        MarketOutlookSourcePosition position,
        Func<MarketOutlookInputState, MarketOutlookInputState> update,
        out MarketOutlookInputState state);
    bool TryGetInputs(MarketOutlookEntityId entityId, out MarketOutlookInputState state);
    void SetCurrent(MarketOutlookReadModel value);
    bool TryGetCurrent(MarketOutlookEntityId entityId, out MarketOutlookReadModel value);
    MarketOutlookHotCacheMetrics GetMetrics();
    void Clear();
}

/// <summary>Process-local immutable Market Outlook input and projection cache.</summary>
public sealed class MarketOutlookHotCache : IMarketOutlookHotCache
{
    sealed class Cell
    {
        public object Gate { get; } = new();
        public MarketOutlookInputState Inputs = new();
    }

    readonly ConcurrentDictionary<MarketOutlookEntityId, Cell> inputs = new();
    readonly ConcurrentDictionary<MarketOutlookEntityId, MarketOutlookReadModel> current = new();
    readonly object fenceGate = new();
    MarketOutlookGenerationFence activeFence;
    long accepted;
    long rejected;
    long projections;
    long queries;
    long lastComponentTicks;
    long lastEsTicks;

    public static MarketOutlookHotCache Shared { get; } = new();

    public MarketOutlookGenerationFence ActiveFence
    {
        get
        {
            lock (fenceGate)
                return activeFence;
        }
    }

    public void Activate(MarketOutlookGenerationFence fence)
    {
        lock (fenceGate)
        {
            if (activeFence == fence)
                return;
            var previous = activeFence;
            activeFence = fence;
            if (!fence.IsValid)
                return;
            if (previous.IsValid
                && previous.GenerationId != fence.GenerationId
                && fence.GenerationId != Guid.Empty)
            {
                inputs.Clear();
                current.Clear();
                return;
            }
            foreach (var key in inputs.Keys.Where(key => !fence.Accepts(key)))
                inputs.TryRemove(key, out _);
            foreach (var key in current.Keys.Where(key => !fence.Accepts(key)))
                current.TryRemove(key, out _);
        }
    }

    public bool TryUpdateInput(
        MarketOutlookEntityId entityId,
        MarketOutlookComponentType component,
        MarketOutlookSourcePosition position,
        Func<MarketOutlookInputState, MarketOutlookInputState> update,
        out MarketOutlookInputState state)
    {
        ArgumentNullException.ThrowIfNull(entityId);
        ArgumentNullException.ThrowIfNull(update);
        lock (fenceGate)
            return TryUpdateInputCore(entityId, component, position, update, out state);
    }

    bool TryUpdateInputCore(
        MarketOutlookEntityId entityId,
        MarketOutlookComponentType component,
        MarketOutlookSourcePosition position,
        Func<MarketOutlookInputState, MarketOutlookInputState> update,
        out MarketOutlookInputState state)
    {
        if (!activeFence.Accepts(entityId))
        {
            Interlocked.Increment(ref rejected);
            state = default!;
            return false;
        }
        var cell = inputs.GetOrAdd(entityId, static id => new Cell
        {
            Inputs = new MarketOutlookInputState { EntityId = id }
        });
        lock (cell.Gate)
        {
            if (cell.Inputs.Positions.TryGetValue(component, out var existing)
                && !position.IsNewerThan(existing))
            {
                Interlocked.Increment(ref rejected);
                state = cell.Inputs;
                return false;
            }
            var next = update(cell.Inputs) with
            {
                EntityId = entityId,
                MarketDataAsOfUtc = position.SourceTimestampUtc > cell.Inputs.MarketDataAsOfUtc
                    ? position.SourceTimestampUtc
                    : cell.Inputs.MarketDataAsOfUtc,
                Positions = cell.Inputs.Positions.SetItem(component, position)
            };
            cell.Inputs = next;
            state = next;
        }
        Interlocked.Increment(ref accepted);
        var nowTicks = DateTime.UtcNow.Ticks;
        if (component == MarketOutlookComponentType.EsTrade)
            Interlocked.Exchange(ref lastEsTicks, nowTicks);
        else
            Interlocked.Exchange(ref lastComponentTicks, nowTicks);
        return true;
    }

    public bool TryGetInputs(MarketOutlookEntityId entityId, out MarketOutlookInputState state)
    {
        lock (fenceGate)
        {
            if (!activeFence.Accepts(entityId) || !inputs.TryGetValue(entityId, out var cell))
            {
                state = default!;
                return false;
            }
            lock (cell.Gate)
                state = cell.Inputs;
            return true;
        }
    }

    public void SetCurrent(MarketOutlookReadModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var id = new MarketOutlookEntityId(value.ContractId, value.ValueDate);
        lock (fenceGate)
        {
            if (!activeFence.Accepts(id))
                return;
            current[id] = value;
            Interlocked.Increment(ref projections);
        }
    }

    public bool TryGetCurrent(MarketOutlookEntityId entityId, out MarketOutlookReadModel value)
    {
        Interlocked.Increment(ref queries);
        lock (fenceGate)
        {
            if (!activeFence.Accepts(entityId))
            {
                value = default!;
                return false;
            }
            return current.TryGetValue(entityId, out value!);
        }
    }

    public MarketOutlookHotCacheMetrics GetMetrics() => new(
        Interlocked.Read(ref accepted),
        Interlocked.Read(ref rejected),
        Interlocked.Read(ref projections),
        Interlocked.Read(ref queries),
        ReadTime(ref lastComponentTicks),
        ReadTime(ref lastEsTicks));

    public void Clear()
    {
        lock (fenceGate)
        {
            inputs.Clear();
            current.Clear();
            activeFence = default;
        }
    }

    static DateTime? ReadTime(ref long ticks)
    {
        var value = Interlocked.Read(ref ticks);
        return value == 0 ? null : new DateTime(value, DateTimeKind.Utc);
    }
}
