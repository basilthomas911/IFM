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

    public bool TryUpdateTrade(LastTradeTickSnapshot snapshot) =>
        TryGetWritableSlot(snapshot.ContractId, snapshot.ValueDate, out var slot)
        && slot.TryUpdateTrade(snapshot);

    public bool TryUpdateQuote(LastQuoteTickSnapshot snapshot) =>
        TryGetWritableSlot(snapshot.ContractId, snapshot.ValueDate, out var slot)
        && slot.TryUpdateQuote(snapshot);

    public bool TryUpdateTradeWithGreeks(LastTradeTickWithGreeksSnapshot snapshot) =>
        TryGetOptionSlot(snapshot.Tick.ContractId, snapshot.Tick.ValueDate, out var slot)
        && slot.TryUpdateTradeWithGreeks(snapshot);

    public bool TryUpdateQuoteWithGreeks(LastQuoteTickWithGreeksSnapshot snapshot) =>
        TryGetOptionSlot(snapshot.Tick.ContractId, snapshot.Tick.ValueDate, out var slot)
        && slot.TryUpdateQuoteWithGreeks(snapshot);

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

    public void Invalidate()
    {
        if (Interlocked.Exchange(ref _active, 0) == 0)
            return;
        foreach (var slot in _slots.Values)
            slot.Invalidate();
    }

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

        private sealed class FuturesReaderHandle(Slot slot) : IFuturesLastPriceReader
        {
            public string FuturesContractId => slot.ContractId;
            public DateOnly ValueDate => slot.ValueDate;
            public bool TryGetLastTrade(out LastTradeTickSnapshot snapshot) =>
                slot.TryReadTrade(out snapshot);
            public bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot) =>
                slot.TryReadQuote(out snapshot);
        }

        private sealed class OptionReaderHandle(Slot slot) : IFuturesOptionLastPriceReader
        {
            public string FuturesOptionContractId => slot.ContractId;
            public DateOnly ValueDate => slot.ValueDate;
            public bool TryGetLastTrade(out LastTradeTickSnapshot snapshot) =>
                slot.TryReadTrade(out snapshot);
            public bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot) =>
                slot.TryReadQuote(out snapshot);
            public bool TryGetLastTradeWithGreeks(
                out LastTradeTickWithGreeksSnapshot snapshot) =>
                slot.TryReadTradeWithGreeks(out snapshot);
            public bool TryGetLastQuoteWithGreeks(
                out LastQuoteTickWithGreeksSnapshot snapshot) =>
                slot.TryReadQuoteWithGreeks(out snapshot);
        }
    }
}
