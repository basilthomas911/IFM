using MessagePack;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>
/// Bounded API-host latest-value state, independent of replaceable dataset workers.
/// Admission changes and writes share one fence. Reader handles remain stable within
/// a value date and return misses while their dataset is recovering.
/// </summary>
public sealed class DatasetWorkerCurrentValues : IDisposable
{
    readonly object gate = new();
    readonly int capacity;
    readonly IDatabentoContractRegistrationRegistry? registrations;
    readonly Dictionary<string, DatasetState> datasets = new(StringComparer.Ordinal);
    readonly Dictionary<string, FuturesMarketPriceSnapshot> prices = new(StringComparer.Ordinal);
    readonly Dictionary<string, FuturesSessionStatisticsSnapshot> statistics = new(StringComparer.Ordinal);
    readonly IDatabentoMarketDataCatalog catalog;
    DatabentoLastPriceStore? lastPrices;

    public DatasetWorkerCurrentValues(IDatabentoContractRegistrationRegistry? registrations = null,
        int capacity = 4096)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.capacity = capacity;
        this.registrations = registrations;
        catalog = new CurrentCatalog(this);
    }

    public DateOnly? ActiveValueDate => GetStatus().ActiveValueDate;
    public bool IsRunning => GetStatus().IsRunning;
    public bool IsFeedUp => GetStatus().IsFeedUp;

    /// <summary>Returns one coherent runtime status even when stop/start is concurrent.</summary>
    public (bool IsRunning, DateOnly? ActiveValueDate, bool IsFeedUp) GetStatus()
    {
        lock (gate)
            return (lastPrices is not null, lastPrices?.ValueDate,
                lastPrices is not null && datasets.Count > 0
                && datasets.Values.All(state => state.Admission.HasValue && state.Healthy));
    }

    /// <summary>
    /// Installs the exact active identity and contract membership. The caller must
    /// close old ingress admission before activation; this mirror independently
    /// fences obsolete output so a delayed write cannot repopulate cleared state.
    /// </summary>
    public void ActivateDataset(DatasetWorkerAdmission identity,
        IReadOnlyList<DatabentoContractRegistration> contracts,
        IDatabentoMarketDataCatalog? referenceCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(contracts);
        if (string.IsNullOrWhiteSpace(identity.Dataset) || identity.ValueDate == default
            || identity.WorkerInstanceId == Guid.Empty || identity.GenerationId == Guid.Empty
            || identity.ManifestRevision < 1)
            throw new ArgumentException("Dataset identity is incomplete.", nameof(identity));
        if (contracts.Count == 0 || contracts.Any(contract =>
                string.IsNullOrWhiteSpace(contract.DomainContractId)
                || contract.AssetTypeId != AssetTypeId.Futures
                || !string.Equals(contract.Dataset, identity.Dataset, StringComparison.Ordinal))
            || contracts.Select(contract => contract.DomainContractId).Distinct(StringComparer.Ordinal).Count() != contracts.Count)
            throw new ArgumentException("Stage 3 requires distinct futures contracts belonging to this dataset.", nameof(contracts));
        lock (gate)
        {
            if (lastPrices is not null && lastPrices.ValueDate != identity.ValueDate)
                throw new MarketDataApiValueDateMismatchException(lastPrices.ValueDate, identity.ValueDate);
            var ids = contracts.Select(contract => contract.DomainContractId).ToHashSet(StringComparer.Ordinal);
            if (datasets.Where(pair => pair.Key != identity.Dataset)
                .SelectMany(pair => pair.Value.Contracts.Keys).Any(ids.Contains))
                throw new ArgumentException("A contract cannot belong to multiple datasets.", nameof(contracts));
            var store = lastPrices ?? new DatabentoLastPriceStore(identity.ValueDate, capacity);
            // Count even retired slots: retained reader identities are never recycled.
            var allocated = datasets.Values.SelectMany(state => state.AllocatedIds).ToHashSet(StringComparer.Ordinal);
            if (allocated.Union(ids).Count() > capacity)
                throw new InvalidOperationException("The supervised current-value reader capacity has been reached.");
            if (datasets.TryGetValue(identity.Dataset, out var previous))
            {
                if (previous.Admission == identity)
                {
                    if (previous.Contracts.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(ids)
                        && contracts.All(contract => previous.Contracts[contract.DomainContractId] == contract)) return;
                    throw new ArgumentException("An admitted manifest is immutable; changing it requires a new identity or revision.", nameof(contracts));
                }
                ClearValues(previous.Contracts.Keys, store);
            }
            foreach (var contract in contracts) store.RegisterContract(contract.DomainContractId, contract.AssetTypeId);
            var state = new DatasetState(identity, contracts, referenceCatalog,
                (previous?.AllocatedIds ?? []).Concat(ids).ToHashSet(StringComparer.Ordinal));
            datasets[identity.Dataset] = state;
            lastPrices = store;
        }
    }

    public void ClearDataset(string dataset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
        lock (gate)
        {
            if (!datasets.TryGetValue(dataset, out var state) || lastPrices is null) return;
            state.Admission = null;
            state.Healthy = false;
            state.LastSequence = 0;
            ClearValues(state.Contracts.Keys, lastPrices);
        }
    }

    /// <summary>Applies a health probe only if its complete worker identity is still current.</summary>
    public void SetDatasetHealth(DatasetWorkerAdmission identity, bool healthy)
    {
        lock (gate)
            if (datasets.TryGetValue(identity.Dataset, out var state) && state.Admission == identity)
                state.Healthy = healthy;
    }

    /// <summary>
    /// Applies already-admitted publications. Exact identity, contract membership,
    /// value date and sequence are checked again atomically with the current write.
    /// Raw batches do not overwrite normalized current values or invent option Greeks.
    /// </summary>
    public bool AcceptPublication(DatasetPublicationEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        lock (gate)
        {
            var identity = new DatasetWorkerAdmission(envelope.Dataset, envelope.ValueDate,
                envelope.WorkerInstanceId, envelope.GenerationId, envelope.ManifestRevision);
            if (lastPrices is null || !datasets.TryGetValue(envelope.Dataset, out var state)
                || state.Admission != identity || envelope.PublicationSequence <= state.LastSequence)
                return false;

            switch (envelope.Kind)
            {
                case DatasetPublicationKind.MarketPrice:
                {
                    var price = MessagePackSerializer.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(envelope.Payload).Price;
                    if (!Matches(state, price.ContractId, price.ValueDate) || price.AssetTypeId != AssetTypeId.Futures)
                        return false;
                    prices[price.ContractId] = price;
                    if (price.Trade is { } trade)
                        lastPrices.TryUpdateTrade(new LastTradeTickSnapshot(price.ContractId, price.ValueDate,
                            trade.LastPrice, trade.LastSize, trade.SourceSequence, trade.EventTimestamp, trade.ReceiveTimestamp));
                    if (price.Quote is { } quote)
                        lastPrices.TryUpdateQuote(new LastQuoteTickSnapshot(price.ContractId, price.ValueDate,
                            quote.BidPrice, quote.BidSize, quote.BidCount, quote.AskPrice, quote.AskSize,
                            quote.AskCount, quote.SourceSequence, quote.EventTimestamp, quote.ReceiveTimestamp));
                    break;
                }
                case DatasetPublicationKind.SessionStatistics:
                {
                    var value = MessagePackSerializer.Deserialize<FuturesSessionStatisticsUpdatedRealtimeEvent>(envelope.Payload).Statistics;
                    if (!Matches(state, value.ContractId, value.ValueDate)) return false;
                    statistics[value.ContractId] = value;
                    break;
                }
                case DatasetPublicationKind.Trade:
                {
                    var value = MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(envelope.Payload);
                    if (!Matches(state, value.TickDataId.ContractId, value.TickDataId.ValueDate)
                        || value.AssetTypeId != AssetTypeId.Futures || value.Dataset != envelope.Dataset) return false;
                    break;
                }
                case DatasetPublicationKind.Quote:
                {
                    var value = MessagePackSerializer.Deserialize<FuturesTickQuoteDataChangedEvent>(envelope.Payload);
                    if (!Matches(state, value.TickDataId.ContractId, value.TickDataId.ValueDate)
                        || value.AssetTypeId != AssetTypeId.Futures || value.Dataset != envelope.Dataset
                        || value.QuoteCount != value.QuoteData.Count) return false;
                    break;
                }
                default: return false;
            }
            state.LastSequence = envelope.PublicationSequence;
            return true;
        }
    }

    public IFuturesLastPriceReader GetFuturesReader(string contractId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        lock (gate)
        {
            var store = lastPrices ?? throw new MarketDataApiNotRunningException();
            if (!datasets.Values.Any(state => state.Contracts.ContainsKey(contractId)))
                throw new MarketDataContractNotFoundException(contractId);
            return store.GetFuturesReader(contractId, store.ValueDate);
        }
    }

    public bool TryGetLastTickPrice(string contractId, out FuturesMarketPriceSnapshot snapshot)
    {
        lock (gate) return prices.TryGetValue(contractId, out snapshot);
    }

    public bool TryGetFuturesSessionStatistics(string contractId, out FuturesSessionStatisticsSnapshot snapshot)
    {
        lock (gate) return statistics.TryGetValue(contractId, out snapshot) && snapshot.IsComplete;
    }

    public IDatabentoMarketDataCatalog GetCatalog()
    {
        lock (gate)
        {
            if (lastPrices is null) throw new MarketDataApiNotRunningException();
            return catalog;
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            lastPrices?.Dispose();
            lastPrices = null;
            datasets.Clear();
            prices.Clear();
            statistics.Clear();
        }
    }

    public void Dispose() => Stop();

    void ClearValues(IEnumerable<string> ids, DatabentoLastPriceStore store)
    {
        var contracts = ids.ToArray();
        store.ResetContracts(contracts);
        foreach (var id in contracts) { prices.Remove(id); statistics.Remove(id); }
    }

    static bool Matches(DatasetState state, string contractId, DateOnly valueDate) =>
        state.Admission?.ValueDate == valueDate && !string.IsNullOrWhiteSpace(contractId)
        && state.Contracts.ContainsKey(contractId);

    sealed class DatasetState(DatasetWorkerAdmission identity,
        IReadOnlyList<DatabentoContractRegistration> contracts,
        IDatabentoMarketDataCatalog? catalog, HashSet<string> allocatedIds)
    {
        public DatasetWorkerAdmission? Admission = identity;
        public long LastSequence;
        public bool Healthy;
        public Dictionary<string, DatabentoContractRegistration> Contracts { get; } =
            contracts.ToDictionary(contract => contract.DomainContractId, StringComparer.Ordinal);
        public IDatabentoMarketDataCatalog? Catalog { get; } = catalog;
        public HashSet<string> AllocatedIds { get; } = allocatedIds;
    }

    sealed class CurrentCatalog(DatasetWorkerCurrentValues owner) : IDatabentoMarketDataCatalog
    {
        public FuturesContractV3ReadModel? FindFutures(string contractId)
        {
            lock (owner.gate)
            {
                foreach (var state in owner.datasets.Values)
                {
                    if (state.Catalog?.FindFutures(contractId) is { } supplied) return supplied;
                    if (!state.Contracts.TryGetValue(contractId, out var registration)) continue;
                    var root = registration.RootSymbol;
                    if (string.IsNullOrWhiteSpace(root)) root = new FuturesContractIdParser(contractId).Symbol;
                    if (owner.registrations?.TryGetOnTheRunFuturesContract(root, out var front) == true
                        && front.ContractId == contractId) return front;
                    if (owner.registrations?.TryGetFuturesTermStructureContracts(root, out var pair) == true)
                    {
                        if (pair.Front.ContractId == contractId) return pair.Front;
                        if (pair.Back.ContractId == contractId) return pair.Back;
                    }
                    throw new MarketDataContractMappingException(contractId,
                        "authoritative contract metadata is unavailable in the supervised API host");
                }
                return null;
            }
        }

        public FuturesOptionContractReadModel? FindFuturesOption(string contractId)
        {
            lock (owner.gate) return owner.datasets.Values.Select(state => state.Catalog?.FindFuturesOption(contractId))
                .FirstOrDefault(value => value is not null);
        }

        public string? FindOptionUnderlying(string contractId)
        {
            lock (owner.gate) return owner.datasets.Values.Select(state => state.Catalog?.FindOptionUnderlying(contractId))
                .FirstOrDefault(value => value is not null);
        }

        public Task<FuturesOptionContractReadModel[]> GetOptionChainAsync(string contractId, DateOnly maturityDate)
        {
            IDatabentoMarketDataCatalog? reference;
            lock (owner.gate) reference = owner.datasets.Values.FirstOrDefault(state => state.Contracts.ContainsKey(contractId))?.Catalog;
            return reference?.GetOptionChainAsync(contractId, maturityDate)
                ?? throw new NotSupportedException("Option-chain reference discovery requires the Stage 4 supervised query integration.");
        }
    }
}
