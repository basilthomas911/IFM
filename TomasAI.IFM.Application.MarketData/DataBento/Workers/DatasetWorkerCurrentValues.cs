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
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>
/// Bounded API-host latest-value state, independent of replaceable dataset workers.
/// Admission changes and writes share one fence. Reader handles remain stable within
/// a value date and return misses while their dataset is recovering.
/// </summary>
public sealed class DatasetWorkerCurrentValues : IDisposable
{
    readonly System.Threading.Lock gate = new();
    readonly int capacity;
    readonly IDatabentoContractRegistrationRegistry? registrations;
    readonly Dictionary<string, DatasetState> datasets = new(StringComparer.Ordinal);
    readonly Dictionary<string, (FuturesMarketPriceSnapshot Price, string Source)> bootstrapPrices = new(StringComparer.Ordinal);
    readonly Dictionary<string, FuturesMarketPriceSnapshot> prices = new(StringComparer.Ordinal);
    readonly Dictionary<string, FuturesSessionStatisticsSnapshot> statistics = new(StringComparer.Ordinal);
    readonly IDatabentoMarketDataCatalog catalog;
    DatabentoLastPriceStore? lastPrices;

    public DatasetWorkerCurrentValues(IDatabentoContractRegistrationRegistry? registrations = null,
        int capacity = 4096)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        this.capacity = capacity;
        this.registrations = registrations;
        catalog = new CurrentCatalog(this);
    }

    public DateOnly? ActiveValueDate => GetStatus().ActiveValueDate;
    public bool IsRunning => GetStatus().IsRunning;
    public bool IsFeedUp => GetStatus().IsFeedUp;

    /// <summary>Whether an admitted dataset worker owns the contract's current tick route.</summary>
    public bool IsTickDataStreamActive(string contractId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        lock (gate)
            return lastPrices is not null && datasets.Values.Any(state =>
                state.Admission.HasValue && state.Contracts.ContainsKey(contractId));
    }

    public FuturesMarketHealthSnapshot GetFuturesMarketHealth(string contractId)
    {
        lock (gate)
        {
            var state = datasets.Values.SingleOrDefault(x => x.Contracts.ContainsKey(contractId));
            var admission = state?.Admission;
            return new(lastPrices is not null, lastPrices is not null && admission.HasValue && state!.Healthy,
                admission is { } id ? $"{id.Dataset}|{id.WorkerInstanceId:N}|{id.GenerationId:N}|{id.ManifestRevision}" : string.Empty,
                lastPrices?.ValueDate, DateTimeOffset.UtcNow, state?.LastSequence ?? 0);
        }
    }

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
            if (datasets.Where(pair => !string.Equals(pair.Key,identity.Dataset,StringComparison.Ordinal))
                .SelectMany(pair => pair.Value.Contracts.Keys).Any(ids.Contains))
                throw new ArgumentException("A contract cannot belong to multiple datasets.", nameof(contracts));
            // Count even retired slots: retained reader identities are never recycled.
            var allocated = datasets.Values.SelectMany(state => state.AllocatedIds).ToHashSet(StringComparer.Ordinal);
            if (allocated.Union(ids, StringComparer.Ordinal).Count() > capacity)
                throw new InvalidOperationException("The supervised current-value reader capacity has been reached.");
            if (datasets.TryGetValue(identity.Dataset, out var previous))
            {
                if (previous.Admission == identity)
                {
                    if (previous.Contracts.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(ids)
                        && contracts.All(contract => previous.Contracts[contract.DomainContractId] == contract)) return;
                    throw new ArgumentException("An admitted manifest is immutable; changing it requires a new identity or revision.", nameof(contracts));
                }
                if (lastPrices is not null) ClearValues(previous.Contracts.Keys, lastPrices);
            }
            DatabentoLastPriceStore? candidate = lastPrices is null
                ? new DatabentoLastPriceStore(identity.ValueDate, capacity) : null;
            try
            {
                var store = lastPrices ?? candidate!;
                foreach (var contract in contracts) store.RegisterContract(contract.DomainContractId, contract.AssetTypeId);
            var state = new DatasetState(identity, contracts, referenceCatalog,
                (previous?.AllocatedIds ?? []).Concat(ids).ToHashSet(StringComparer.Ordinal));
            datasets[identity.Dataset] = state;
                lastPrices = store;
                candidate = null; // Ownership transfers to this current-value registry.
            }
            finally { candidate?.Dispose(); }
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep native resource ownership and recovery failure ordering together during this storage compatibility refactor.")]
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
                case DatasetPublicationKind.OptionTradeEvidence:
                    {
                        var evidence = MessagePackSerializer.Deserialize<Pricing.OptionTradeEvidence>(envelope.Payload);
                        evidence.Validate();
                        if (!string.Equals(evidence.Source.Dataset,envelope.Dataset,StringComparison.Ordinal) || evidence.Source.ValueDate != envelope.ValueDate
                            || evidence.Source.GenerationId != envelope.GenerationId
                            || evidence.Context is { } context && !state.Contracts.ContainsKey(context.Contract.UnderlyingContractId))
                            return false;
                        break;
                    }
                case DatasetPublicationKind.OptionQuoteObservation:
                    {
                        var value = MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(envelope.Payload);
                        if (value.OptionMarketPriceObservation is not { PriceBasis: OptionMarketPriceBasis.QuoteMidpoint } observation
                            || value.AssetTypeId != AssetTypeId.FuturesOption || !string.Equals(value.Dataset,envelope.Dataset,StringComparison.Ordinal)
                            || !Matches(state, observation.UnderlyingContractId, value.TickDataId.ValueDate)
                            || !string.Equals(value.EntityId.ContractId,value.TickDataId.ContractId,StringComparison.Ordinal)
                            || !string.Equals(observation.OptionTickData.ContractId,value.TickDataId.ContractId,StringComparison.Ordinal)
                            || observation.OptionTickData.ValueDate != envelope.ValueDate
                            || value.PublisherId == 0 || value.InstrumentId == 0 || observation.SourceSequence <= 0
                            || observation.SourceSequence != value.TickDataId.SequenceId
                            || observation.EventAtUtc.Kind != DateTimeKind.Utc
                            || !double.IsFinite(observation.OptionTickData.OptionPrice) || observation.OptionTickData.OptionPrice <= 0
                            || observation.OptionTickData.BidPrice <= 0 || observation.OptionTickData.AskPrice < observation.OptionTickData.BidPrice)
                            return false;
                        break;
                    }
                case DatasetPublicationKind.MarketPrice:
                    {
                        var marketPriceEvent = MessagePackSerializer.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(envelope.Payload);
                        var price = marketPriceEvent.Price;
                        if (marketPriceEvent.VwapCheckpoint is not null)
                        {
                            if (!Matches(state, marketPriceEvent.EntityId.ContractId,
                                    marketPriceEvent.EntityId.ValueDate)
                                || marketPriceEvent.EntityId.AssetTypeId != AssetTypeId.Futures)
                                return false;
                            break;
                        }
                        if (!Matches(state, price.ContractId, price.ValueDate) || price.AssetTypeId != AssetTypeId.Futures)
                            return false;
                        prices[price.ContractId] = price;
                        if (price.Trade is { } latestTrade && latestTrade.LastPrice > 0 && bootstrapPrices.ContainsKey(price.ContractId)
                            && latestTrade.NormalizedTradeAction is not (NormalizedTradeAction.Cancel or NormalizedTradeAction.Clear)
                            && (latestTrade.NormalizedTradeConditionFlags & NormalizedTradeConditionFlags.UndefinedPrice) == 0
                            && latestTrade.EventTimestamp > bootstrapPrices[price.ContractId].Price.Trade!.Value.EventTimestamp)
                            bootstrapPrices[price.ContractId] = (price with { Quote = null, Trade = latestTrade with
                                { NormalizedTradeConditionFlags = latestTrade.NormalizedTradeConditionFlags | NormalizedTradeConditionFlags.Replay } }, "RetainedLiveCache");
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
                case DatasetPublicationKind.TradeReplayBatch:
                    {
                        var value = MessagePackSerializer.Deserialize<FuturesTradeReplayBatchRealtimeEvent>(envelope.Payload);
                        if (!Matches(state, value.EntityId.ContractId, value.EntityId.ValueDate))
                            return false;
                        break;
                    }
                case DatasetPublicationKind.Trade:
                    {
                        var value = MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(envelope.Payload);
                        if (!Matches(state, value.TickDataId.ContractId, value.TickDataId.ValueDate)
                            || value.AssetTypeId != AssetTypeId.Futures || !string.Equals(value.Dataset,envelope.Dataset,StringComparison.Ordinal)) return false;
                        break;
                    }
                case DatasetPublicationKind.Quote:
                    {
                        var value = MessagePackSerializer.Deserialize<FuturesTickQuoteDataChangedEvent>(envelope.Payload);
                        if (!Matches(state, value.TickDataId.ContractId, value.TickDataId.ValueDate)
                            || value.AssetTypeId != AssetTypeId.Futures || !string.Equals(value.Dataset,envelope.Dataset,StringComparison.Ordinal)
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
        lock (gate)
        {
            if (prices.TryGetValue(contractId, out snapshot) && snapshot.Trade is not null) return true;
            if (lastPrices is not null && datasets.Values.Any(state => state.Admission.HasValue && state.Contracts.ContainsKey(contractId))
                && bootstrapPrices.TryGetValue(contractId, out var seed))
            { snapshot = seed.Price; return true; }
            return prices.ContainsKey(contractId);
        }
    }

    /// <summary>Seeds a historical estimate without publishing a live event or advancing feed health/sequence.</summary>
    public bool SeedBootstrapPrice(DateOnly activeDate, FuturesMarketPriceSnapshot price, string source)
    {
        if (price.AssetTypeId != AssetTypeId.Futures || price.Trade is not { } trade || trade.LastPrice <= 0 || trade.EventTimestamp > DateTimeOffset.UtcNow
            || trade.NormalizedTradeAction is NormalizedTradeAction.Cancel or NormalizedTradeAction.Clear
            || (trade.NormalizedTradeConditionFlags & NormalizedTradeConditionFlags.UndefinedPrice) != 0) return false;
        lock (gate)
        {
            if (lastPrices?.ValueDate != activeDate || !datasets.Values.Any(state => state.Admission.HasValue && state.Contracts.ContainsKey(price.ContractId))) return false;
            if (prices.TryGetValue(price.ContractId, out var live) && live.Trade is not null) return false;
            if (bootstrapPrices.TryGetValue(price.ContractId, out var old) && old.Price.Trade!.Value.EventTimestamp >= trade.EventTimestamp) return false;
            bootstrapPrices[price.ContractId] = (price, source);
            return true;
        }
    }

    /// <summary>Reports the original seed and its source independently of live feed evidence.</summary>
    public bool TryGetBootstrapPrice(string contractId, out FuturesMarketPriceSnapshot price, out string source)
    {
        lock (gate)
        {
            if (bootstrapPrices.TryGetValue(contractId, out var value)) { price = value.Price; source = value.Source; return true; }
            price = default; source = string.Empty; return false;
        }
    }

    public bool TryGetFuturesSessionStatistics(string contractId, out FuturesSessionStatisticsSnapshot snapshot)
    {
        lock (gate) return statistics.TryGetValue(contractId, out snapshot) && snapshot.IsComplete;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1024", Justification = "This operation reads a coherent runtime snapshot under the admission lock; retain the explicit method contract.")]
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
            bootstrapPrices.Clear();
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
                        && string.Equals(front.ContractId,contractId,StringComparison.Ordinal)) return front;
                    if (owner.registrations?.TryGetFuturesTermStructureContracts(root, out var pair) == true)
                    {
                        if (string.Equals(pair.Front.ContractId,contractId,StringComparison.Ordinal)) return pair.Front;
                        if (string.Equals(pair.Back.ContractId,contractId,StringComparison.Ordinal)) return pair.Back;
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the existing public contract and named argument compatibility; changing parameter names or order would break callers.")]
        public string? FindOptionUnderlying(string contractId)
        {
            lock (owner.gate) return owner.datasets.Values.Select(state => state.Catalog?.FindOptionUnderlying(contractId))
                .FirstOrDefault(value => value is not null);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the existing public contract and named argument compatibility; changing parameter names or order would break callers.")]
        public Task<FuturesOptionContractReadModel[]> GetOptionChainAsync(string contractId, DateOnly maturityDate)
        {
            IDatabentoMarketDataCatalog? reference;
            lock (owner.gate) reference = owner.datasets.Values.FirstOrDefault(state => state.Contracts.ContainsKey(contractId))?.Catalog;
            return reference?.GetOptionChainAsync(contractId, maturityDate)
                ?? throw new NotSupportedException("Option-chain reference discovery requires the Stage 4 supervised query integration.");
        }

        public Task<FuturesOptionContractReadModel[]> GetOptionChainBySymbolAsync(
            string underlyingSymbol,
            DateOnly maturityDate)
        {
            IDatabentoMarketDataCatalog? reference;
            lock (owner.gate)
                reference = owner.datasets.Values.FirstOrDefault(state =>
                    state.Catalog is not null && state.Contracts.Values.Any(registration =>
                        string.Equals(registration.RootSymbol, underlyingSymbol, StringComparison.OrdinalIgnoreCase)))?.Catalog;
            return reference?.GetOptionChainBySymbolAsync(underlyingSymbol, maturityDate)
                ?? throw new NotSupportedException(
                    $"No supervised Databento catalogue is available for underlying symbol '{underlyingSymbol}'.");
        }

        public Task<OptionContractExpiryReadModel[]> DiscoverOptionContractExpiriesAsync(
            string underlyingSymbol,
            DateOnly fromExpiry,
            DateOnly throughExpiry,
            CancellationToken cancellationToken = default)
        {
            IDatabentoMarketDataCatalog? reference;
            lock (owner.gate)
                reference = owner.datasets.Values.FirstOrDefault(state =>
                    state.Catalog is not null && state.Contracts.Values.Any(registration =>
                        string.Equals(registration.RootSymbol, underlyingSymbol, StringComparison.OrdinalIgnoreCase)))?.Catalog;
            return reference?.DiscoverOptionContractExpiriesAsync(
                    underlyingSymbol, fromExpiry, throughExpiry, cancellationToken)
                ?? throw new NotSupportedException(
                    $"No supervised Databento catalogue is available for underlying symbol '{underlyingSymbol}'.");
        }

        public Task<FuturesOptionContractReadModel[]> GetOptionChainByRootAsync(
            string underlyingSymbol,
            string providerRoot,
            DateOnly maturityDate)
        {
            IDatabentoMarketDataCatalog? reference;
            lock (owner.gate)
                reference = owner.datasets.Values.FirstOrDefault(state =>
                    state.Catalog is not null && state.Contracts.Values.Any(registration =>
                        string.Equals(registration.RootSymbol, underlyingSymbol, StringComparison.OrdinalIgnoreCase)))?.Catalog;
            return reference?.GetOptionChainByRootAsync(underlyingSymbol, providerRoot, maturityDate)
                ?? throw new NotSupportedException(
                    $"No supervised Databento catalogue is available for underlying symbol '{underlyingSymbol}'.");
        }
    }
}
