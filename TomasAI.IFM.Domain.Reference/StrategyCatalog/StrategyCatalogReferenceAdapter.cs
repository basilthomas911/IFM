using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.StrategyCatalog;

public sealed class StrategyCatalogReferenceAdapter(IDbContextFactory db) : IStrategyCatalogReferences
{
    public async Task ValidateProductAsync(CatalogProduct product, CancellationToken cancellationToken)
    {
        var snapshot = await db.ReferenceDb.GetInstrumentDefinitionSnapshotAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("No published instrument-definition snapshot is available.");
        var matches = 0;
        foreach (var family in new[] { TradeStrategyFamilyType.Futures, TradeStrategyFamilyType.FuturesOption })
        {
            var rows = await db.ReferenceDb.GetInstrumentDefinitionSymbolsAsync(snapshot.Id, family, cancellationToken).ConfigureAwait(false);
            matches += rows.Count(x => x.Id == product.ProductId && string.Equals(x.Symbol,product.Symbol,StringComparison.Ordinal) && string.Equals(x.Currency,product.Currency,StringComparison.Ordinal) && string.Equals(x.Exchange,product.Exchange,StringComparison.Ordinal));
        }
        if (matches != 1) throw new ArgumentException("Product identity, symbol, exchange and currency do not match the authoritative instrument catalog.", nameof(product));
    }

    public async Task ValidateLegacyFamilyAsync(CatalogLegacyFamily family, StrategyCatalogDefinition deployment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deployment);
        ArgumentNullException.ThrowIfNull(family);
        var original = await (db.ReferenceDb.GetTradeStrategyFamilyAsync(family.Id, family.Version, cancellationToken)).ConfigureAwait(false);
        if (original is null || original.TimeFrame != deployment.Horizon || !deployment.Products.Any(p => string.Equals(p.Symbol,original.Symbol,StringComparison.Ordinal) && string.Equals(p.Currency,original.Currency,StringComparison.Ordinal) && (original.Exchange.Length == 0 || string.Equals(p.Exchange,original.Exchange,StringComparison.Ordinal))))
            throw new ArgumentException("Legacy mapping does not match the original product/timeframe. It grants no Fund permission.",nameof(deployment));
    }
}
