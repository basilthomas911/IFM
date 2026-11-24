using System.Buffers;
using MessagePack;
using MessagePack.Formatters;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

/// <summary>Original trade fields and persisted provenance; batching never substitutes receipt timestamps.</summary>
[MessagePackObject]
public readonly record struct TradeStorageRecord(
    [property: Key(0)] FuturesTickTradeData Trade,
    [property: Key(1)] long SequenceId,
    [property: Key(2)] long AggregationTicks,
    [property: Key(3)] Guid EventId,
    [property: Key(4)] Guid CommandId,
    [property: Key(5)] long EventLogId,
    [property: Key(6)] string AggregateId,
    [property: Key(7)] string EventSource,
    [property: Key(8)] long ReceivedTicks,
    [property: Key(9)] long AcceptanceOrdinal);

/// <summary>A bounded leased segment; copies share one idempotent return owner.</summary>
[MessagePackFormatter(typeof(TradeStorageSegmentFormatter))]
public readonly struct TradeStorageSegment : IDisposable
{
    public const int MaximumCount = 256;
    readonly TradeStorageRecord[] buffer;
    readonly IDisposable? owner;
    readonly int start;
    public TradeStorageSegment(TradeStorageRecord[] buffer, int count) : this(buffer, count, null) { }
    internal TradeStorageSegment(TradeStorageRecord[] buffer, int count, IDisposable? owner)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (count is < 1 or > MaximumCount || count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
        this.buffer = buffer; Count = count; this.owner = owner; start = 0;
    }
    TradeStorageSegment(TradeStorageRecord[] buffer, int start, int count)
    { this.buffer = buffer; this.start = start; Count = count; owner = null; }
    public int Count { get; }
    public ReadOnlySpan<TradeStorageRecord> Items => buffer.AsSpan(start, Count);
    internal Span<TradeStorageRecord> WritableItems => buffer.AsSpan(start, Count);
    public TradeStorageSegment Slice(int offset, int count)
    {
        if (offset < 0 || count <= 0 || offset > Count - count) throw new ArgumentOutOfRangeException(nameof(offset));
        return new(buffer, start + offset, count);
    }
    public void Dispose() => owner?.Dispose();
}

/// <summary>Bounds decoded array capacity and returns it only after the final consumer finishes.</summary>
public sealed class TradeStorageSegmentFormatter : IMessagePackFormatter<TradeStorageSegment>
{
    static readonly ArrayPool<TradeStorageRecord> Pool = ArrayPool<TradeStorageRecord>.Create(TradeStorageSegment.MaximumCount, 16);
    sealed class Lease(TradeStorageRecord[] buffer) : IDisposable
    {
        TradeStorageRecord[]? value = buffer;
        public void Dispose() { if (Interlocked.Exchange(ref value, null) is { } returned) Pool.Return(returned, clearArray: true); }
    }
    internal static TradeStorageSegment Rent(int count)
    {
        if (count is < 1 or > TradeStorageSegment.MaximumCount) throw new ArgumentOutOfRangeException(nameof(count));
        var buffer = Pool.Rent(count);
        return new(buffer, count, new Lease(buffer));
    }
    public void Serialize(ref MessagePackWriter writer, TradeStorageSegment value, MessagePackSerializerOptions options)
    {
        if (value.Count is < 1 or > TradeStorageSegment.MaximumCount) throw new MessagePackSerializationException("Invalid trade segment count.");
        writer.WriteArrayHeader(value.Count);
        var formatter = options.Resolver.GetFormatterWithVerify<TradeStorageRecord>();
        foreach (ref readonly var record in value.Items) formatter.Serialize(ref writer, record, options);
    }
    public TradeStorageSegment Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var count = reader.ReadArrayHeader();
        if (count is < 1 or > TradeStorageSegment.MaximumCount) throw new MessagePackSerializationException("Invalid trade segment count.");
        options.Security.DepthStep(ref reader);
        var buffer = Pool.Rent(count);
        var lease = new Lease(buffer);
        try
        {
            var formatter = options.Resolver.GetFormatterWithVerify<TradeStorageRecord>();
            for (var index = 0; index < count; index++) buffer[index] = formatter.Deserialize(ref reader, options);
            return new(buffer, count, lease);
        }
        catch { lease.Dispose(); throw; }
        finally { reader.Depth--; }
    }
}

/// <summary>Versioned raw-storage payload, independent of domain event streams and live workflow delivery.</summary>
[MessagePackObject]
public sealed record TradeStorageBatch(
    [property: Key(0)] int Version,
    [property: Key(1)] Guid BatchId,
    [property: Key(2)] AssetTypeId AssetType,
    [property: Key(3)] string ContractId,
    [property: Key(4)] DateOnly ValueDate,
    [property: Key(5)] string Dataset,
    [property: Key(6)] DateOnly DefinitionDate,
    [property: Key(7)] ushort PublisherId,
    [property: Key(8)] uint InstrumentId,
    [property: Key(9)] Guid SourceEpoch,
    [property: Key(10)] TradeStorageSegment Records) : IDisposable
{
    public const int CurrentVersion = 1;
    public void Dispose() => Records.Dispose();
}
