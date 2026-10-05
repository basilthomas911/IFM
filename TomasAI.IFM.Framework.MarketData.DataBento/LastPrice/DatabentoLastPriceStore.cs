using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;

/// <summary>
/// Bounded, epoch-local latest-value storage. Writers are serialized per slot;
/// readers use a sequence lock and never take the writer lock.
/// </summary>
public sealed class DatabentoLastPriceStore : IDatabentoLastPriceStore
{
    private readonly ConcurrentDictionary<string, Slot> _slots =
        new(StringComparer.Ordinal);
    private readonly object _registrationSync = new();
    private int _active = 1;

    /// <summary>Initializes a new DatabentoLastPriceStore instance.</summary>
    /// <param name="valueDate">The trading value date associated with the data.</param>
    /// <param name="capacity">The maximum capacity of the buffer, store, or queue.</param>
    public DatabentoLastPriceStore(DateOnly valueDate, int capacity)
    {
        if (valueDate == default)
            throw new ArgumentOutOfRangeException(nameof(valueDate));
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        ValueDate = valueDate;
        Capacity = capacity;
    }

    public DateOnly ValueDate { get; }
    public int Capacity { get; }
    public int Count => _slots.Count;
    public bool IsActive => Volatile.Read(ref _active) != 0;

    /// <summary>Registers a contract in the value-date-specific last-price store.</summary>
    /// <param name="contractId">The futures or option contract identifier.</param>
    /// <param name="assetTypeId">The asset type identifying futures or futures options.</param>
    public void RegisterContract(
        string contractId,
        AssetTypeId assetTypeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (assetTypeId is not (AssetTypeId.Futures or AssetTypeId.FuturesOption))
            throw new ArgumentOutOfRangeException(nameof(assetTypeId));
        ThrowIfInactive();

        lock (_registrationSync)
        {
            if (_slots.TryGetValue(contractId, out var existing))
            {
                if (existing.AssetTypeId != assetTypeId)
                    throw new InvalidOperationException(
                        $"Contract '{contractId}' is already registered as {existing.AssetTypeId}.");
                return;
            }

            if (_slots.Count >= Capacity)
                throw new InvalidOperationException(
                    $"The DataBento last-price capacity of {Capacity} has been reached.");

            if (!_slots.TryAdd(contractId, new Slot(contractId, ValueDate, assetTypeId)))
                throw new InvalidOperationException(
                    $"Contract '{contractId}' could not be registered.");
        }
    }

    /// <summary>Attempts to update the cached trade price with the supplied trade snapshot.</summary>
    /// <param name="snapshot">The market data snapshot to update, or the snapshot returned when the read succeeds.</param>
    /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
    public bool TryUpdateTrade(LastTradeTickSnapshot snapshot) =>
        TryGetWritableSlot(snapshot.ContractId, snapshot.ValueDate, out var slot)
        && slot.TryUpdateTrade(snapshot);

    /// <summary>Attempts to update the cached bid and ask prices with the supplied quote snapshot.</summary>
    /// <param name="snapshot">The market data snapshot to update, or the snapshot returned when the read succeeds.</param>
    /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
    public bool TryUpdateQuote(LastQuoteTickSnapshot snapshot) =>
        TryGetWritableSlot(snapshot.ContractId, snapshot.ValueDate, out var slot)
        && slot.TryUpdateQuote(snapshot);

    /// <summary>Attempts to update the option trade snapshot and its Greeks.</summary>
    /// <param name="snapshot">The market data snapshot to update, or the snapshot returned when the read succeeds.</param>
    /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
    public bool TryUpdateTradeWithGreeks(LastTradeTickWithGreeksSnapshot snapshot) =>
        TryGetOptionSlot(snapshot.Tick.ContractId, snapshot.Tick.ValueDate, out var slot)
        && slot.TryUpdateTradeWithGreeks(snapshot);

    /// <summary>Attempts to update the option quote snapshot and its Greeks.</summary>
    /// <param name="snapshot">The market data snapshot to update, or the snapshot returned when the read succeeds.</param>
    /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
    public bool TryUpdateQuoteWithGreeks(LastQuoteTickWithGreeksSnapshot snapshot) =>
        TryGetOptionSlot(snapshot.Tick.ContractId, snapshot.Tick.ValueDate, out var slot)
        && slot.TryUpdateQuoteWithGreeks(snapshot);

    /// <summary>Creates a futures price reader for the specified contract and value date.</summary>
    /// <param name="futuresContractId">The underlying futures contract identifier.</param>
    /// <param name="valueDate">The trading value date associated with the data.</param>
    /// <returns>The futures reader result.</returns>
    public IFuturesLastPriceReader GetFuturesReader(
        string futuresContractId,
        DateOnly valueDate)
    {
        var slot = GetSlot(futuresContractId, valueDate);
        if (slot.AssetTypeId != AssetTypeId.Futures)
            throw new InvalidOperationException(
                $"Contract '{futuresContractId}' is not a futures contract.");
        return slot.FuturesReader;
    }

    /// <summary>Creates an option price reader for the specified contract and value date.</summary>
    /// <param name="futuresOptionContractId">The futures option contract identifier.</param>
    /// <param name="valueDate">The trading value date associated with the data.</param>
    /// <returns>The futures option reader result.</returns>
    public IFuturesOptionLastPriceReader GetFuturesOptionReader(
        string futuresOptionContractId,
        DateOnly valueDate)
    {
        var slot = GetSlot(futuresOptionContractId, valueDate);
        if (slot.AssetTypeId != AssetTypeId.FuturesOption)
            throw new InvalidOperationException(
                $"Contract '{futuresOptionContractId}' is not a futures-option contract.");
        return slot.OptionReader;
    }

    /// <summary>Invalidates stored prices so existing readers cannot use them as current market data.</summary>
    public void Invalidate()
    {
        if (Interlocked.Exchange(ref _active, 0) == 0)
            return;
        foreach (var slot in _slots.Values)
            slot.Invalidate();
    }

    /// <summary>
    /// Clears the latest values for a replaced dataset generation while preserving the
    /// epoch-scoped reader handles registered for those contracts.
    /// </summary>
    /// <param name="contractIds">The contract ids.</param>
    public void ResetContracts(IEnumerable<string> contractIds)
    {
        ArgumentNullException.ThrowIfNull(contractIds);
        ThrowIfInactive();

        foreach (var contractId in contractIds.Distinct(StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
            if (_slots.TryGetValue(contractId, out var slot))
                slot.Reset();
        }
    }

    /// <summary>Releases the resources owned by this instance.</summary>
    public void Dispose() => Invalidate();

    private Slot GetSlot(string contractId, DateOnly valueDate)
    {
        ValidateIdentity(contractId, valueDate);
        ThrowIfInactive();
        return _slots.TryGetValue(contractId, out var slot)
            ? slot
            : throw new KeyNotFoundException(
                $"Contract '{contractId}' is not registered in the {ValueDate:yyyy-MM-dd} epoch.");
    }

    private bool TryGetWritableSlot(
        string contractId,
        DateOnly valueDate,
        out Slot slot)
    {
        slot = null!;
        return IsActive
            && valueDate == ValueDate
            && _slots.TryGetValue(contractId, out slot!);
    }

    private bool TryGetOptionSlot(
        string contractId,
        DateOnly valueDate,
        out Slot slot) =>
        TryGetWritableSlot(contractId, valueDate, out slot)
        && slot.AssetTypeId == AssetTypeId.FuturesOption;

    private void ValidateIdentity(string contractId, DateOnly valueDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (valueDate != ValueDate)
            throw new ArgumentException(
                $"Value date {valueDate:yyyy-MM-dd} does not match store epoch {ValueDate:yyyy-MM-dd}.",
                nameof(valueDate));
    }

    private void ThrowIfInactive()
    {
        if (!IsActive)
            throw new ObjectDisposedException(nameof(DatabentoLastPriceStore));
    }

    private sealed class Slot
    {
        private readonly object _writeSync = new();
        private int _version;
        private bool _active = true;
        private bool _hasTrade;
        private bool _hasQuote;
        private bool _hasTradeWithGreeks;
        private bool _hasQuoteWithGreeks;
        private LastTradeTickSnapshot _trade;
        private LastQuoteTickSnapshot _quote;
        private LastTradeTickWithGreeksSnapshot _tradeWithGreeks;
        private LastQuoteTickWithGreeksSnapshot _quoteWithGreeks;

        /// <summary>Initializes a new Slot instance.</summary>
        /// <param name="contractId">The futures or option contract identifier.</param>
        /// <param name="valueDate">The trading value date associated with the data.</param>
        /// <param name="assetTypeId">The asset type identifying futures or futures options.</param>
        internal Slot(string contractId, DateOnly valueDate, AssetTypeId assetTypeId)
        {
            ContractId = contractId;
            ValueDate = valueDate;
            AssetTypeId = assetTypeId;
            FuturesReader = new FuturesReaderHandle(this);
            OptionReader = new OptionReaderHandle(this);
        }

        internal string ContractId { get; }
        internal DateOnly ValueDate { get; }
        internal AssetTypeId AssetTypeId { get; }
        internal IFuturesLastPriceReader FuturesReader { get; }
        internal IFuturesOptionLastPriceReader OptionReader { get; }

        internal bool TryUpdateTrade(LastTradeTickSnapshot snapshot)
        {
            lock (_writeSync)
            {
                if (!_active || IsOlderOrEqual(_hasTrade, _trade.SourceSequence,
                        _trade.EventTimestamp, snapshot.SourceSequence, snapshot.EventTimestamp))
                    return false;
                var odd = BeginWrite();
                try
                {
                    _trade = snapshot;
                    _hasTrade = true;
                    if (_hasTradeWithGreeks
                        && _tradeWithGreeks.Tick.SourceSequence != snapshot.SourceSequence)
                    {
                        _hasTradeWithGreeks = false;
                        _tradeWithGreeks = default;
                    }
                }
                finally { EndWrite(odd); }
                return true;
            }
        }

        internal bool TryUpdateQuote(LastQuoteTickSnapshot snapshot)
        {
            lock (_writeSync)
            {
                if (!_active || IsOlderOrEqual(_hasQuote, _quote.SourceSequence,
                        _quote.EventTimestamp, snapshot.SourceSequence, snapshot.EventTimestamp))
                    return false;
                var odd = BeginWrite();
                try
                {
                    _quote = snapshot;
                    _hasQuote = true;
                    if (_hasQuoteWithGreeks
                        && _quoteWithGreeks.Tick.SourceSequence != snapshot.SourceSequence)
                    {
                        _hasQuoteWithGreeks = false;
                        _quoteWithGreeks = default;
                    }
                }
                finally { EndWrite(odd); }
                return true;
            }
        }

        internal bool TryUpdateTradeWithGreeks(LastTradeTickWithGreeksSnapshot snapshot)
        {
            lock (_writeSync)
            {
                if (!_active
                    || IsOlder(_hasTrade, _trade.SourceSequence,
                        _trade.EventTimestamp, snapshot.Tick.SourceSequence,
                        snapshot.Tick.EventTimestamp)
                    || IsOlderOrEqual(_hasTradeWithGreeks,
                        _tradeWithGreeks.Tick.SourceSequence,
                        _tradeWithGreeks.Tick.EventTimestamp,
                        snapshot.Tick.SourceSequence,
                        snapshot.Tick.EventTimestamp))
                    return false;
                var odd = BeginWrite();
                try
                {
                    _trade = snapshot.Tick;
                    _hasTrade = true;
                    _tradeWithGreeks = snapshot;
                    _hasTradeWithGreeks = true;
                }
                finally { EndWrite(odd); }
                return true;
            }
        }

        internal bool TryUpdateQuoteWithGreeks(LastQuoteTickWithGreeksSnapshot snapshot)
        {
            lock (_writeSync)
            {
                if (!_active
                    || IsOlder(_hasQuote, _quote.SourceSequence,
                        _quote.EventTimestamp, snapshot.Tick.SourceSequence,
                        snapshot.Tick.EventTimestamp)
                    || IsOlderOrEqual(_hasQuoteWithGreeks,
                        _quoteWithGreeks.Tick.SourceSequence,
                        _quoteWithGreeks.Tick.EventTimestamp,
                        snapshot.Tick.SourceSequence,
                        snapshot.Tick.EventTimestamp))
                    return false;
                var odd = BeginWrite();
                try
                {
                    _quote = snapshot.Tick;
                    _hasQuote = true;
                    _quoteWithGreeks = snapshot;
                    _hasQuoteWithGreeks = true;
                }
                finally { EndWrite(odd); }
                return true;
            }
        }

        internal bool TryReadTrade(out LastTradeTickSnapshot snapshot)
        {
            while (true)
            {
                var before = Volatile.Read(ref _version);
                if ((before & 1) != 0)
                {
                    Thread.SpinWait(1);
                    continue;
                }
                var active = _active;
                var hasValue = _hasTrade;
                var value = _trade;
                if (before == Volatile.Read(ref _version))
                {
                    snapshot = value;
                    return active && hasValue;
                }
            }
        }

        internal bool TryReadQuote(out LastQuoteTickSnapshot snapshot)
        {
            while (true)
            {
                var before = Volatile.Read(ref _version);
                if ((before & 1) != 0)
                {
                    Thread.SpinWait(1);
                    continue;
                }
                var active = _active;
                var hasValue = _hasQuote;
                var value = _quote;
                if (before == Volatile.Read(ref _version))
                {
                    snapshot = value;
                    return active && hasValue;
                }
            }
        }

        internal bool TryReadTradeWithGreeks(out LastTradeTickWithGreeksSnapshot snapshot)
        {
            while (true)
            {
                var before = Volatile.Read(ref _version);
                if ((before & 1) != 0)
                {
                    Thread.SpinWait(1);
                    continue;
                }
                var active = _active;
                var hasValue = _hasTradeWithGreeks;
                var value = _tradeWithGreeks;
                if (before == Volatile.Read(ref _version))
                {
                    snapshot = value;
                    return active && hasValue;
                }
            }
        }

        internal bool TryReadQuoteWithGreeks(out LastQuoteTickWithGreeksSnapshot snapshot)
        {
            while (true)
            {
                var before = Volatile.Read(ref _version);
                if ((before & 1) != 0)
                {
                    Thread.SpinWait(1);
                    continue;
                }
                var active = _active;
                var hasValue = _hasQuoteWithGreeks;
                var value = _quoteWithGreeks;
                if (before == Volatile.Read(ref _version))
                {
                    snapshot = value;
                    return active && hasValue;
                }
            }
        }

        internal void Invalidate()
        {
            lock (_writeSync)
            {
                if (!_active) return;
                var odd = BeginWrite();
                try
                {
                    _active = false;
                    _hasTrade = _hasQuote = false;
                    _hasTradeWithGreeks = _hasQuoteWithGreeks = false;
                    _trade = default;
                    _quote = default;
                    _tradeWithGreeks = default;
                    _quoteWithGreeks = default;
                }
                finally { EndWrite(odd); }
            }
        }

        internal void Reset()
        {
            lock (_writeSync)
            {
                if (!_active) return;
                var odd = BeginWrite();
                try
                {
                    _hasTrade = _hasQuote = false;
                    _hasTradeWithGreeks = _hasQuoteWithGreeks = false;
                    _trade = default;
                    _quote = default;
                    _tradeWithGreeks = default;
                    _quoteWithGreeks = default;
                }
                finally { EndWrite(odd); }
            }
        }

        private int BeginWrite() => Interlocked.Increment(ref _version);

        private void EndWrite(int oddVersion) =>
            Volatile.Write(ref _version, oddVersion + 1);

        private static bool IsOlderOrEqual(
            bool hasCurrent,
            long currentSequence,
            DateTimeOffset currentTimestamp,
            long candidateSequence,
            DateTimeOffset candidateTimestamp) =>
            hasCurrent && (candidateSequence < currentSequence
                || (candidateSequence == currentSequence
                    && candidateTimestamp <= currentTimestamp));

        private static bool IsOlder(
            bool hasCurrent,
            long currentSequence,
            DateTimeOffset currentTimestamp,
            long candidateSequence,
            DateTimeOffset candidateTimestamp) =>
            hasCurrent && (candidateSequence < currentSequence
                || (candidateSequence == currentSequence
                    && candidateTimestamp < currentTimestamp));

        /// <summary>Initializes a new FuturesReaderHandle instance.</summary>
        /// <param name="slot">The contract cache slot read by this handle.</param>
        private sealed class FuturesReaderHandle(Slot slot) : IFuturesLastPriceReader
        {
            public string FuturesContractId => slot.ContractId;
            public DateOnly ValueDate => slot.ValueDate;
            /// <summary>Attempts to read the most recent accepted trade snapshot.</summary>
            /// <param name="snapshot">The market data snapshot to update, or the snapshot returned when the read succeeds.</param>
            /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
            public bool TryGetLastTrade(out LastTradeTickSnapshot snapshot) =>
                slot.TryReadTrade(out snapshot);
            /// <summary>Attempts to read the most recent accepted quote snapshot.</summary>
            /// <param name="snapshot">The market data snapshot to update, or the snapshot returned when the read succeeds.</param>
            /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
            public bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot) =>
                slot.TryReadQuote(out snapshot);
        }

        /// <summary>Initializes a new OptionReaderHandle instance.</summary>
        /// <param name="slot">The contract cache slot read by this handle.</param>
        private sealed class OptionReaderHandle(Slot slot) : IFuturesOptionLastPriceReader
        {
            public string FuturesOptionContractId => slot.ContractId;
            public DateOnly ValueDate => slot.ValueDate;
            /// <summary>Attempts to read the most recent accepted trade snapshot.</summary>
            /// <param name="snapshot">The market data snapshot to update, or the snapshot returned when the read succeeds.</param>
            /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
            public bool TryGetLastTrade(out LastTradeTickSnapshot snapshot) =>
                slot.TryReadTrade(out snapshot);
            /// <summary>Attempts to read the most recent accepted quote snapshot.</summary>
            /// <param name="snapshot">The market data snapshot to update, or the snapshot returned when the read succeeds.</param>
            /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
            public bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot) =>
                slot.TryReadQuote(out snapshot);
            /// <summary>Attempts to read the most recent option trade snapshot including Greeks.</summary>
            /// <param name="snapshot">The market data snapshot to update, or the snapshot returned when the read succeeds.</param>
            /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
            public bool TryGetLastTradeWithGreeks(
                out LastTradeTickWithGreeksSnapshot snapshot) =>
                slot.TryReadTradeWithGreeks(out snapshot);
            /// <summary>Attempts to read the most recent option quote snapshot including Greeks.</summary>
            /// <param name="snapshot">The market data snapshot to update, or the snapshot returned when the read succeeds.</param>
            /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
            public bool TryGetLastQuoteWithGreeks(
                out LastQuoteTickWithGreeksSnapshot snapshot) =>
                slot.TryReadQuoteWithGreeks(out snapshot);
        }
    }
}
