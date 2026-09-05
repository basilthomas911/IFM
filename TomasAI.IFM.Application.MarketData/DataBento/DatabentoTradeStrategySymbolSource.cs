using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Databento;

/// <summary>Provider-wide product discovery, independent of seeded families and realtime subscriptions.</summary>
public sealed class DatabentoTradeStrategySymbolSource(IDatabentoFeedFactory feeds, DatabentoMarketDataRuntimeOptions options,
    TimeProvider timeProvider, ILogger<DatabentoTradeStrategySymbolSource>? logger = null) : ITradeStrategySymbolSource
{
    readonly SemaphoreSlim _gate = new(1, 1);
    DateTimeOffset _expires;
    Dictionary<TradeStrategyFamilyType, TradeStrategyProduct[]>? _snapshot;

    public async Task<IReadOnlyList<TradeStrategyProduct>> DiscoverAsync(TradeStrategyFamilyType family, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (family is not (TradeStrategyFamilyType.Futures or TradeStrategyFamilyType.FuturesOption))
            throw new NotSupportedException($"Unsupported trade strategy family: {family}.");
        if (options.FeedOptions.DataSource != FeedDataSourceMode.DatabentoLive)
            throw new InvalidOperationException("Product discovery requires Databento metadata; synthetic feeds cannot populate the catalog.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_snapshot is null || _expires <= timeProvider.GetUtcNow())
            {
                var snapshot = await DiscoverAllAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                _snapshot = snapshot;
                _expires = timeProvider.GetUtcNow().AddMinutes(5);
            }
            // Retain compact product summaries only, not the dataset's full option-chain records.
            return [.. _snapshot[family]];
        }
        finally { _gate.Release(); }
    }

    async Task<Dictionary<TradeStrategyFamilyType, TradeStrategyProduct[]>> DiscoverAllAsync(CancellationToken token)
    {
        var configured = options.TradeStrategySymbolDatasets.Count == 0
            ? [options.FeedOptions.Dataset] : options.TradeStrategySymbolDatasets;
        if (configured.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("A symbol-discovery dataset is required.");
        var datasets = configured.Select(x => x.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToArray();
        HashSet<TradeStrategyProduct> futures = [], optionsProducts = [];
        foreach (var dataset in datasets)
        {
            token.ThrowIfCancellationRequested();
            var query = feeds.CreateMarketDataQueries(options.FeedOptions with { Dataset = dataset });
            var definitions = await Task.Run(() => query.GetDatasetDefinitions(options.ProviderQueryTimeout), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (definitions.Count == 0) throw new InvalidOperationException($"No instrument definitions returned for dataset '{dataset}'.");
            int invalid = 0, unresolved = 0;
            bool Current(ContractDetail detail)
            {
                var now = timeProvider.GetUtcNow();
                if (detail.ActivationTimestampNanoseconds is { } activation && activation / 1_000_000_000UL > (ulong)now.ToUnixTimeSeconds()) return false;
                if (detail.ExpirationTimestampNanoseconds is { } expiry) return expiry / 1_000_000_000UL > (ulong)now.ToUnixTimeSeconds();
                if (detail.MaturityDate is { } maturity) return maturity >= DateOnly.FromDateTime(now.UtcDateTime);
                ++invalid; return false;
            }
            var currentFutures = definitions.Where(x => x.ContractKind == ContractKind.Future && Current(x)).ToArray();
            var byId = currentFutures.ToLookup(x => x.Instrument);
            var byName = currentFutures.ToLookup(x => (x.Instrument.PublisherId, x.RawSymbol));
            void Add(HashSet<TradeStrategyProduct> target, TradeStrategyFamilyType productFamily, ContractDetail underlying, ContractDetail priced)
            {
                var product = new TradeStrategyProduct(productFamily, underlying.Ticker?.Trim() ?? "",
                    priced.Currency?.Trim().ToUpperInvariant() ?? "", priced.Exchange?.Trim().ToUpperInvariant() ?? "");
                try { product.Validate(); target.Add(product); }
                catch (ArgumentException) { ++invalid; }
            }
            foreach (var future in currentFutures)
            {
                token.ThrowIfCancellationRequested();
                Add(futures, TradeStrategyFamilyType.Futures, future, future);
            }
            foreach (var option in definitions.Where(x => x.ContractKind is ContractKind.CallOption or ContractKind.PutOption))
            {
                token.ThrowIfCancellationRequested();
                if (!Current(option)) continue;
                var matches = option.UnderlyingInstrumentId != 0
                    ? byId[new(option.Instrument.PublisherId, option.UnderlyingInstrumentId)].ToArray()
                    : string.IsNullOrWhiteSpace(option.Underlying) ? [] : byName[(option.Instrument.PublisherId, option.Underlying)].ToArray();
                // Options on spreads/non-futures, unresolved IDs and ambiguous links are not
                // outright-futures products. Never guess an underlying from the option root.
                if (matches.Length != 1) { ++unresolved; continue; }
                Add(optionsProducts, TradeStrategyFamilyType.FuturesOption, matches[0], option);
            }
            if (invalid != 0 || unresolved != 0)
                logger?.LogWarning("Symbol discovery {Dataset}: excluded {Invalid} incomplete definitions and {Unresolved} options without a unique current outright-futures underlying.", dataset, invalid, unresolved);
        }
        if (futures.Count == 0 && optionsProducts.Count == 0)
            throw new InvalidOperationException("No eligible products with complete Symbol, Currency and Exchange were discovered.");
        return new()
        {
            [TradeStrategyFamilyType.Futures] = futures.ToArray(),
            [TradeStrategyFamilyType.FuturesOption] = optionsProducts.ToArray()
        };
    }
}
