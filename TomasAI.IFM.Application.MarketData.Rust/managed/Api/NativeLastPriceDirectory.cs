using System.Collections.Frozen;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Application.MarketData.Rust.OptionChains;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Date-scoped stable reader handles over native state. Construction
/// and reads acquire no subscription. Futures are fixed by the qualified catalog;
/// option handles follow current monitoring authority across reference changes
/// and physical resets. Retiring an epoch makes all previously issued handles
/// unavailable, without retaining a second managed copy of tick/pricing state.</summary>
public sealed class NativeLastPriceDirectory : IDatabentoLastPriceReaderProvider, IDisposable
{
    readonly DateOnly valueDate;
    readonly NativeOptionChainRuntime scopes;
    readonly FrozenDictionary<string, Future> futures;
    readonly Dictionary<string, Option> options = new(StringComparer.Ordinal);
    readonly object gate = new();
    readonly int capacity;
    int retired;

    /// <summary>Creates one bounded directory. Supplied futures must already be
    /// uniquely qualified to a native lane; the runtime remains owned by its epoch.</summary>
    public NativeLastPriceDirectory(NativeMarketDataRuntime runtime, NativeOptionChainRuntime scopes,
        DateOnly valueDate, IEnumerable<(string ContractId, MarketDataLane Lane)> futures, int capacity = 4096)
    {
        ArgumentNullException.ThrowIfNull(runtime); ArgumentNullException.ThrowIfNull(scopes); ArgumentNullException.ThrowIfNull(futures);
        if (valueDate == default || runtime.LiveValueDate != valueDate) throw new ArgumentOutOfRangeException(nameof(valueDate));
        if (capacity is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(capacity));
        var entries = futures.Take(capacity + 1).ToArray();
        if (entries.Length > capacity || entries.Select(x => x.ContractId).Distinct(StringComparer.Ordinal).Count() != entries.Length)
            throw new ArgumentException("Futures reader registrations must be unique and fit the epoch capacity.", nameof(futures));
        this.scopes = scopes; this.valueDate = valueDate; this.capacity = capacity;
        this.futures = entries.ToFrozenDictionary(x => x.ContractId,
            x => new Future(this, new(runtime, x.Lane, x.ContractId, valueDate)), StringComparer.Ordinal);
    }

    /// <summary>Number of issued contract handles, bounded by the epoch capacity.</summary>
    public int Count { get { lock (gate) return futures.Count + options.Count; } }
    /// <summary>Whether this date-scoped reader directory is still owned.</summary>
    public bool IsActive => Volatile.Read(ref retired) == 0;
    void Validate(string id, DateOnly date)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (date != valueDate) throw new ArgumentException("Reader ValueDate differs from its API epoch.", nameof(date));
        ObjectDisposedException.ThrowIf(!IsActive, this);
    }
    /// <inheritdoc/>
    public IFuturesLastPriceReader GetFuturesReader(string futuresContractId, DateOnly valueDate)
    {
        Validate(futuresContractId, valueDate);
        if(futures.TryGetValue(futuresContractId,out var reader))return reader;
        lock(gate)
            if(options.ContainsKey(futuresContractId)||scopes.IsMonitoredContract(futuresContractId))
                throw new InvalidOperationException("The contract is a futures-option contract.");
        throw new KeyNotFoundException("The futures contract is not registered in this native epoch.");
    }
    /// <inheritdoc/>
    public IFuturesOptionLastPriceReader GetFuturesOptionReader(string futuresOptionContractId, DateOnly valueDate)
    {
        Validate(futuresOptionContractId, valueDate);
        lock (gate)
        {
            Validate(futuresOptionContractId, valueDate);
            if (options.TryGetValue(futuresOptionContractId, out var reader)) return reader;
            if (futures.ContainsKey(futuresOptionContractId)) throw new InvalidOperationException("The contract is a futures contract.");
            if (scopes.FindMonitoredQuote(futuresOptionContractId) is not { } binding || !binding.IsCurrent())
                throw new KeyNotFoundException("The option has no current native monitoring registration.");
            if (futures.Count + options.Count >= capacity) throw new InvalidOperationException("Native epoch reader capacity reached.");
            reader = new(this, futuresOptionContractId); options.Add(futuresOptionContractId, reader); return reader;
        }
    }
    /// <summary>Invalidates every issued handle. No task, socket or native runtime
    /// is disposed here; the epoch must separately join those owned resources.</summary>
    public void Dispose() => Interlocked.Exchange(ref retired, 1);

    sealed class Future(NativeLastPriceDirectory owner, NativeFuturesPriceReader source) : IFuturesLastPriceReader
    {
        public string FuturesContractId => source.FuturesContractId;
        public DateOnly ValueDate => owner.valueDate;
        public bool TryGetLastTrade(out LastTradeTickSnapshot snapshot)
        { snapshot = default; if (!owner.IsActive || !source.TryGetLastTrade(out var value) || !owner.IsActive) return false; snapshot = value; return true; }
        public bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot)
        { snapshot = default; if (!owner.IsActive || !source.TryGetLastQuote(out var value) || !owner.IsActive) return false; snapshot = value; return true; }
    }
    sealed class Option(NativeLastPriceDirectory owner, string id) : IFuturesOptionLastPriceReader
    {
        public string FuturesOptionContractId => id;
        public DateOnly ValueDate => owner.valueDate;
        NativeMonitoredOptionQuote? Binding() => owner.IsActive ? owner.scopes.FindMonitoredQuote(id) : null;
        bool Current(NativeMonitoredOptionQuote binding) => owner.IsActive && binding.IsCurrent()
            && ReferenceEquals(owner.scopes.FindMonitoredQuote(id), binding);
        public bool TryGetLastTrade(out LastTradeTickSnapshot snapshot)
        { snapshot = default; var binding = Binding(); if (binding is null || !Current(binding) || !binding.Reader.TryGetLastTrade(out var value) || !Current(binding)) return false; snapshot = value; return true; }
        public bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot)
        { snapshot = default; var binding = Binding(); if (binding is null || !Current(binding) || !binding.Reader.TryGetLastQuote(out var value) || !Current(binding)) return false; snapshot = value; return true; }
        public bool TryGetLastTradeWithGreeks(out LastTradeTickWithGreeksSnapshot snapshot)
        { snapshot = default; var binding = Binding(); if (binding is null || !Current(binding) || !binding.Reader.TryGetLastTradeWithGreeks(out var value) || !Current(binding)) return false; snapshot = value; return true; }
        public bool TryGetLastQuoteWithGreeks(out LastQuoteTickWithGreeksSnapshot snapshot)
        { snapshot = default; var binding = Binding(); if (binding is null || !Current(binding) || !binding.Reader.TryGetLastQuoteWithGreeks(out var value) || !Current(binding)) return false; snapshot = value; return true; }
    }
}
