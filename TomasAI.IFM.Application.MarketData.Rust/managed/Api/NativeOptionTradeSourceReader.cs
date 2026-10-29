using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Owned source-time inputs. These remain valid after a context refresh,
/// native reset, or release of the reader's reusable transfer buffer.</summary>
public sealed record NativeCapturedOptionTrade(OptionTradeSource Source, WorkerOptionDefinition Definition,
    OptionPricingQuote? Underlying, DateTimeOffset CalculatedAtUtc, DateTimeOffset QualifiedUntilUtc,
    Guid ContextIdentity, Guid PhysicalGeneration);

/// <summary>Transactional native queue reader. Only the synchronous copy and
/// deserialization hold its lock; no pooled memory escapes into async processing.
/// Non-trade records count as consumed so callers can drain until truly caught up.</summary>
public sealed class NativeOptionTradeSourceReader : IDisposable
{
    const int ManifestCapacity = 4 * 1024 * 1024;
    readonly NativeMarketDataRuntime runtime;
    readonly Guid initialGeneration;
    readonly Func<Guid>? currentGeneration;
    Guid generation=>currentGeneration?.Invoke()??initialGeneration;
    readonly DateOnly valueDate;
    readonly object gate = new();
    readonly NativeOptionPublication[] records = new NativeOptionPublication[16];
    readonly Dictionary<ManifestKey, LinkedListNode<(ManifestKey Key, WorkerOptionDefinition Value, int Bytes)>> cache = new();
    readonly LinkedList<(ManifestKey Key, WorkerOptionDefinition Value, int Bytes)> recent = new();
    int cachedBytes;
    byte[]? storage = ArrayPool<byte>.Shared.Rent(ManifestCapacity);
    readonly record struct ManifestKey(Guid Context, ulong A, ulong B, ulong C, ulong D);

    /// <summary>Uses the authoritative API generation and ValueDate only.</summary>
    public NativeOptionTradeSourceReader(NativeMarketDataRuntime runtime, Guid generation, DateOnly valueDate, Func<Guid>? currentGeneration=null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (generation == Guid.Empty || valueDate == default) throw new ArgumentException("API generation and ValueDate are required.");
        this.runtime = runtime; this.initialGeneration = generation; this.currentGeneration=currentGeneration; this.valueDate = valueDate;
    }

    static Guid Identity(ulong low, ulong high)
    {
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, low); BinaryPrimitives.WriteUInt64LittleEndian(bytes[8..], high);
        return new(bytes);
    }

    WorkerOptionDefinition Definition(Guid identity, ReadOnlyMemory<byte> bytes)
    {
        Span<byte> hash = stackalloc byte[32]; SHA256.HashData(bytes.Span, hash);
        var key = new ManifestKey(identity, BinaryPrimitives.ReadUInt64LittleEndian(hash), BinaryPrimitives.ReadUInt64LittleEndian(hash[8..]),
            BinaryPrimitives.ReadUInt64LittleEndian(hash[16..]), BinaryPrimitives.ReadUInt64LittleEndian(hash[24..]));
        if (cache.TryGetValue(key, out var hit)) { recent.Remove(hit); recent.AddLast(hit); return hit.Value.Value; }
        var definition = MessagePackBinarySerializer.Shared.Deserialize<WorkerOptionDefinition>(bytes)
            ?? throw new InvalidDataException("Missing frozen option trade definition.");
        // Bound retained serialized-input weight as well as entry count. The
        // deserialized immutable graph is shared only until LRU eviction; this
        // cache cannot retain 2,048 maximum-size manifests indefinitely.
        while (cache.Count >= 2048 || cachedBytes + bytes.Length > 16 * 1024 * 1024)
        {
            var oldest = recent.First!; cache.Remove(oldest.Value.Key); cachedBytes -= oldest.Value.Bytes; recent.RemoveFirst();
        }
        cache.Add(key, recent.AddLast((key, definition, bytes.Length))); cachedBytes += bytes.Length; return definition;
    }

    /// <summary>Consumes at most sixteen records. Returned trades own all data;
    /// consumed can be positive with zero trades because quotes/statistics have
    /// already updated native state and are delivered by separate latest views.</summary>
    public unsafe int Drain(string scope, Span<NativeCapturedOptionTrade?> destination, out int consumed)
        =>Drain(scope,destination,Span<LivePublication>.Empty,out consumed);
    /// <summary>Also copies each consumed raw contribution for exact transient
    /// routes. Raw slots correspond to consumed records; captured trades remain
    /// compacted in destination. No decoder or reusable manifest borrow escapes.</summary>
    public unsafe int Drain(string scope,Span<NativeCapturedOptionTrade?> destination,Span<LivePublication> raw,out int consumed)
    {
        if (destination.Length is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(destination));
        if(!raw.IsEmpty&&raw.Length<destination.Length)throw new ArgumentOutOfRangeException(nameof(raw));
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(storage is null, this);
            var generation=this.generation;
            consumed = runtime.DrainOptionScopeTradePublications(scope, generation, records.AsSpan(0, destination.Length),
                storage.AsSpan(0, ManifestCapacity), out _);
            var count = 0;
            for (var index = 0; index < consumed; index++)
            {
                var row = records[index];if(!raw.IsEmpty)raw[index]=row.Publication; if ((row.Flags & 8) == 0) continue;
                if ((row.Flags & 5) != 5) throw new InvalidDataException("Actual option trade has no captured reference metadata; recovery is required.");
                var identity = Identity(row.ContextLow, row.ContextHigh);
                var source = row.Publication.Source;
                var physical = Identity(source.GenerationLow, source.GenerationHigh);
                if (identity == Guid.Empty || physical == Guid.Empty) throw new InvalidDataException("Option trade generation is absent.");
                var definition = Definition(identity, storage.AsMemory(checked((int)row.ManifestOffset), checked((int)row.ManifestLength)));
                var context = definition.Pricing; var contract = context.Contract;
                if (context.GenerationId != generation || contract.InstrumentId != source.Instrument || contract.PublisherId != (ushort)source.Fields[4]
                    || (source.Fields[4] >> 16 & 2) != 0 || source.Fields[0] < 0 || source.Fields[1] < 0 || source.Fields[2] < 0)
                    throw new InvalidDataException("Frozen option trade identity does not match its source record.");
                var trade = new OptionTradeSource(contract.Dataset, contract.PublisherId, source.Instrument, contract.ContractId,
                    valueDate, source.Fields[5] / 1_000_000_000m, checked((uint)source.Fields[6]), source.Fields[0],
                    source.Fields[1], source.Fields[2], generation, contract.RawSymbol);
                var underlying = (row.Flags & 2) == 0 ? null : NativeOptionPricingCache.Quote(row.Underlying, contract.UnderlyingContractId);
                if (underlying is not null && underlying.GenerationId != generation) throw new InvalidDataException("Frozen underlying generation differs.");
                destination[count++] = new(trade, definition, underlying, DateTimeOffset.UnixEpoch.AddTicks(row.At),
                    DateTimeOffset.UnixEpoch.AddTicks(row.QualifiedUntil), identity, physical);
            }
            return count;
        }
    }

    /// <summary>Returns only reusable transfer storage. Already returned immutable
    /// source records and definitions remain owned by their callers.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            if (storage is null) return;
            ArrayPool<byte>.Shared.Return(storage, clearArray: true); storage = null; cache.Clear(); recent.Clear(); cachedBytes = 0;
        }
    }
}
