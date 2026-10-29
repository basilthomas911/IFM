using System.Buffers.Binary;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Stable, non-consuming option reader over a qualified native scope.
/// Source and pricing identities are checked together; resetting transport retires
/// earlier enrichment without invalidating the public reader handle. No read
/// acquires a subscription, waits for a solver, or substitutes quote IV for trade IV.</summary>
public sealed class NativeOptionPriceReader : IFuturesOptionLastPriceReader
{
    readonly NativeMarketDataRuntime runtime;
    readonly NativeOptionPricingCache pricing;
    readonly string scope;
    readonly Guid generation;
    readonly Func<PreparedOptionPricingContext> preparation;
    readonly TimeProvider clock;
    readonly object evidenceGate = new();
    TradeEvidence? tradeEvidence;
    sealed record TradeEvidence(Guid Physical, LastTradeTickWithGreeksSnapshot Snapshot);
    /// <inheritdoc/>
    public string FuturesOptionContractId { get; }
    /// <inheritdoc/>
    public DateOnly ValueDate { get; }

    /// <summary>Binds an existing scope to the API clock and an atomically
    /// replaceable immutable pricing context. The accessor must not perform I/O.</summary>
    public NativeOptionPriceReader(NativeMarketDataRuntime runtime, string scope, Guid generation,
        DateOnly valueDate, Func<PreparedOptionPricingContext> preparation, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(runtime); ArgumentNullException.ThrowIfNull(preparation); ArgumentNullException.ThrowIfNull(clock);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        if (generation == Guid.Empty || valueDate == default || runtime.LiveValueDate != valueDate) throw new ArgumentException("An active API date and logical options generation are required.");
        var entry = preparation();
        if (entry.Context.GenerationId != generation) throw new ArgumentException("Prepared pricing belongs to a different generation.", nameof(preparation));
        this.runtime = runtime; this.scope = scope; this.generation = generation; this.preparation = preparation; this.clock = clock;
        pricing = new(runtime); ValueDate = valueDate; FuturesOptionContractId = entry.Context.Contract.ContractId;
    }
    PreparedOptionPricingContext Context()
    {
        var entry = preparation();
        if (entry.Context.GenerationId != generation || entry.Context.Contract.ContractId != FuturesOptionContractId)
            throw new InvalidOperationException("A stable option reader cannot be rebound to another logical contract.");
        return entry;
    }
    bool State(PreparedOptionPricingContext entry, out LivePublication value, out Guid physical)
    {
        try
        {
            if(runtime.ReadOptionOwnerGeneration()!=generation){value=default;physical=default;return false;}
            value = runtime.ReadOptionScopeState(scope, entry.Context.Contract.InstrumentId, entry.Context.Contract.PublisherId);
            Span<byte> bytes = stackalloc byte[16];
            BinaryPrimitives.WriteUInt64LittleEndian(bytes,value.Source.GenerationLow); BinaryPrimitives.WriteUInt64LittleEndian(bytes[8..],value.Source.GenerationHigh);
            physical = new(bytes); return value.Flags != 0 && physical != Guid.Empty;
        }
        catch (ObjectDisposedException) { value = default; physical = default; return false; }
    }
    unsafe bool Quote(LivePublication value, out LastQuoteTickSnapshot snapshot)
    {
        if ((value.Flags & 2) == 0) { snapshot = default; return false; }
        var f=value.Fields;
        snapshot=new(FuturesOptionContractId,ValueDate,f[5]!=0?f[6]/1_000_000_000m:null,checked((uint)f[7]),checked((uint)f[8]),
            f[9]!=0?f[10]/1_000_000_000m:null,checked((uint)f[11]),checked((uint)f[12]),f[1],DateTimeOffset.UnixEpoch.AddTicks(f[2]),DateTimeOffset.UnixEpoch.AddTicks(f[3]))
            { LocalReceivedAtUtc=DateTimeOffset.UnixEpoch.AddTicks(f[4]) };return true;
    }
    unsafe bool Trade(LivePublication value, out LastTradeTickSnapshot snapshot)
    {
        if((value.Flags&4)==0){snapshot=default;return false;}
        var f=value.Fields;
        snapshot=new(FuturesOptionContractId,ValueDate,f[17]/1_000_000_000m,checked((uint)f[18]),f[14],DateTimeOffset.UnixEpoch.AddTicks(f[15]),DateTimeOffset.UnixEpoch.AddTicks(f[16]));return true;
    }
    /// <inheritdoc/>
    public bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot)
    {
        return TryGetLastQuote(out snapshot,out _);
    }
    /// <summary>Reads the quote and its exact physical identity together for
    /// event deduplication across a transport reset.</summary>
    public bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot,out Guid physicalGeneration)
    {
        if(State(Context(),out var value,out physicalGeneration))return Quote(value,out snapshot);snapshot=default;return false;
    }
    /// <inheritdoc/>
    public bool TryGetLastTrade(out LastTradeTickSnapshot snapshot)
    {
        if(State(Context(),out var value,out _))return Trade(value,out snapshot);snapshot=default;return false;
    }
    /// <inheritdoc/>
    public bool TryGetLastQuoteWithGreeks(out LastQuoteTickWithGreeksSnapshot snapshot)
        =>TryGetLastQuoteWithGreeks(out snapshot,out _);
    /// <summary>Returns the exact physical identity with the atomic enriched
    /// source so publication cannot suppress a replacement as an old duplicate.</summary>
    public bool TryGetLastQuoteWithGreeks(out LastQuoteTickWithGreeksSnapshot snapshot,out Guid physicalGeneration)
    {
        snapshot=default;physicalGeneration=default;var entry=Context();
        if(!State(entry,out var first,out var physical)||!Quote(first,out var tick))return false;
        var result=pricing.ReadQuoteGreeks(scope,generation,entry,clock.GetUtcNow());
        if(result is not { } calculated||calculated.PhysicalGeneration!=physical||calculated.Quote.Sequence!=tick.SourceSequence
            ||calculated.Quote.EventAtUtc!=tick.EventTimestamp||calculated.Quote.Bid!=tick.BidPrice||calculated.Quote.Ask!=tick.AskPrice
            ||calculated.Quote.BidSize!=tick.BidSize||calculated.Quote.AskSize!=tick.AskSize)return false;
        // Source may advance while the separately owned pricing cache is read.
        // Recheck before returning; no older result can acquire a newer tick label.
        if(Context().Identity!=entry.Identity||!State(entry,out var last,out var current)||current!=physical||!Quote(last,out var latest)||latest!=tick)return false;
        snapshot=new(tick,calculated.Greeks);physicalGeneration=physical;return true;
    }
    /// <summary>Publishes only the calculation acknowledged by the durable trade
    /// evidence writer. A completed old attempt cannot attach to a replacement
    /// source. Call this after CalculateAndRetainAsync, never on enqueue alone.</summary>
    public bool RetainAcknowledgedTrade(Guid physicalGeneration, Guid pricingIdentity, LastTradeTickWithGreeksSnapshot snapshot)
    {
        if(snapshot.Tick.ContractId!=FuturesOptionContractId||snapshot.Tick.ValueDate!=ValueDate)throw new ArgumentException("Trade evidence belongs to another contract/date.",nameof(snapshot));
        if(snapshot.Greeks.PriceSource!=OptionGreeksPriceSource.Trade)throw new ArgumentException("Actual-trade evidence is required.",nameof(snapshot));
        lock(evidenceGate)
        {
            var entry=Context();
            if(entry.Identity!=pricingIdentity||!State(entry,out var state,out var current)||current!=physicalGeneration||!Trade(state,out var latest)||latest!=snapshot.Tick)return false;
            Interlocked.Exchange(ref tradeEvidence,new(physicalGeneration,snapshot));return true;
        }
    }
    /// <inheritdoc/>
    public bool TryGetLastTradeWithGreeks(out LastTradeTickWithGreeksSnapshot snapshot)
    {
        snapshot=default;var evidence=Volatile.Read(ref tradeEvidence);
        if(evidence is null||!State(Context(),out var state,out var physical)||physical!=evidence.Physical
            ||!Trade(state,out var tick)||tick!=evidence.Snapshot.Tick)return false;
        snapshot=evidence.Snapshot;return true;
    }
}
