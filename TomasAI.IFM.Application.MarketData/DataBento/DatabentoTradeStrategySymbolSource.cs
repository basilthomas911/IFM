using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Databento;

/// <summary>Explicit, bounded product universe; metadata only, never starts a tick subscription.</summary>
public sealed record DatabentoTradeStrategyProductConfiguration(string Symbol, string Dataset, string[] OptionRoots);

public sealed class DatabentoTradeStrategySymbolSource(IDatabentoFeedFactory feeds, DatabentoMarketDataRuntimeOptions options,
    TimeProvider timeProvider) : ITradeStrategySymbolSource
{
    public async Task<IReadOnlyList<TradeStrategyProduct>> DiscoverAsync(TradeStrategyFamilyType family, CancellationToken cancellationToken)
    {
        if (family is not (TradeStrategyFamilyType.Futures or TradeStrategyFamilyType.FuturesOption))
            throw new NotSupportedException($"Unsupported trade strategy family: {family}.");
        if (options.FeedOptions.DataSource != FeedDataSourceMode.DatabentoLive)
            throw new InvalidOperationException("Product discovery requires Databento metadata; synthetic feeds cannot populate the catalog.");
        var configured = options.TradeStrategyProducts;
        if (configured.Count == 0) throw new InvalidOperationException("No Databento TradeStrategyProducts universe is configured.");
        if (configured.Count > 100) throw new InvalidOperationException("TradeStrategyProducts is limited to 100 configured products.");
        List<TradeStrategyProduct> products = [];
        foreach (var config in configured)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(config.Symbol) || string.IsNullOrWhiteSpace(config.Dataset) || config.OptionRoots is null || config.OptionRoots.Length > 32)
                throw new InvalidOperationException("A configured product requires Symbol, Dataset and a bounded OptionRoots list.");
            var symbol = config.Symbol.Trim().ToUpperInvariant();
            var queries = feeds.CreateMarketDataQueries(options.FeedOptions with { Dataset = config.Dataset });
            var futures = (await Read(queries, $"{symbol}.FUT", cancellationToken).ConfigureAwait(false))
                .Where(x => x.ContractKind == ContractKind.Future && IsCurrent(x)).ToArray();
            if (futures.Length == 0) throw new InvalidOperationException($"No current futures definitions for '{symbol}' in '{config.Dataset}'.");
            foreach (var future in futures)
            {
                var product = Product(family, future, future);
                if (product.Symbol != symbol) throw new InvalidOperationException($"Definition '{future.RawSymbol}' has unexpected product '{product.Symbol}'.");
            }
            if (family == TradeStrategyFamilyType.Futures)
            {
                products.AddRange(futures.Select(x => Product(family, x, x)));
                continue;
            }
            var productCountBeforeOptions = products.Count;
            foreach (var root in config.OptionRoots.Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException($"Empty option root for '{symbol}'.");
                var definitions = await Read(queries, $"{root.Trim().ToUpperInvariant()}.OPT", cancellationToken).ConfigureAwait(false);
                foreach (var option in definitions.Where(x => x.ContractKind is ContractKind.CallOption or ContractKind.PutOption && IsCurrent(x)))
                {
                    // Match the underlying instrument, never assume option root == futures root.
                    var matches = futures.Where(x => x.Instrument.PublisherId == option.Instrument.PublisherId &&
                        (option.UnderlyingInstrumentId != 0
                            ? x.Instrument.InstrumentId == option.UnderlyingInstrumentId
                            : !string.IsNullOrWhiteSpace(option.Underlying) && x.RawSymbol == option.Underlying)).ToArray();
                    if (matches.Length != 1) throw new InvalidOperationException($"Option '{option.RawSymbol}' has an unresolved or ambiguous underlying for '{symbol}'.");
                    products.Add(Product(family, matches[0], option));
                }
            }
            if (products.Count == productCountBeforeOptions)
                throw new InvalidOperationException($"No current option definitions for configured product '{symbol}'.");
        }
        return products.Distinct().ToArray();
    }

    async Task<IReadOnlyList<ContractDetail>> Read(IDatabentoMarketDataQueries queries, string parent, CancellationToken cancellationToken)
    {
        var result = await Task.Run(() => queries.GetContractDetails(parent, options.ProviderQueryTimeout), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
    bool IsCurrent(ContractDetail detail)
    {
        var now = timeProvider.GetUtcNow();
        if (detail.ActivationTimestampNanoseconds is { } activation && activation / 1_000_000_000UL > (ulong)now.ToUnixTimeSeconds()) return false;
        if (detail.ExpirationTimestampNanoseconds is { } expiration) return expiration / 1_000_000_000UL > (ulong)now.ToUnixTimeSeconds();
        if (detail.MaturityDate is { } maturity) return maturity >= DateOnly.FromDateTime(now.UtcDateTime);
        throw new InvalidOperationException($"Expiry metadata is missing for '{detail.RawSymbol}'.");
    }
    static TradeStrategyProduct Product(TradeStrategyFamilyType family, ContractDetail underlying, ContractDetail pricedInstrument)
    {
        var product = new TradeStrategyProduct(family, underlying.Ticker?.Trim() ?? "", pricedInstrument.Currency?.Trim().ToUpperInvariant() ?? "", pricedInstrument.Exchange?.Trim().ToUpperInvariant() ?? "");
        product.Validate(); // No settlement-currency or configured fallback.
        return product;
    }
}
