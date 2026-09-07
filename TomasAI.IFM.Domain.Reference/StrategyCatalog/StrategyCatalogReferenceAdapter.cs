using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.StrategyCatalog;

public sealed class StrategyCatalogReferenceAdapter(IMarketDataApi marketData, IDbContextFactory db) : IStrategyCatalogReferences
{
    public async Task ValidateProductAsync(CatalogProduct product, CancellationToken ct)
    {
        var matches = 0;
        foreach (var family in new[] { TradeStrategyFamilyType.Futures, TradeStrategyFamilyType.FuturesOption })
        {
            var rows = await marketData.GetTradeStrategySymbolsAsync(family, ct);
            if (!rows.Success || rows.Value is null) throw new InvalidOperationException(rows.ErrorMessage ?? "Product catalog unavailable.");
            matches += rows.Value.Count(x => x.Id == product.ProductId && x.Symbol == product.Symbol && x.Currency == product.Currency && x.Exchange == product.Exchange);
        }
        if (matches != 1) throw new ArgumentException("Product identity, symbol, exchange and currency do not match the authoritative instrument catalog.");
    }

    public async Task ValidateLegacyFamilyAsync(CatalogLegacyFamily family, StrategyCatalogDefinition deployment, CancellationToken ct)
    {
        var original = await db.ReferenceDb.GetTradeStrategyFamilyAsync(family.Id, family.Version, ct);
        if (original is null || original.TimeFrame != deployment.Horizon || !deployment.Products.Any(p => p.Symbol == original.Symbol && p.Currency == original.Currency && (original.Exchange.Length == 0 || p.Exchange == original.Exchange)))
            throw new ArgumentException("Legacy mapping does not match the original product/timeframe. It grants no Fund permission.");
    }
}
