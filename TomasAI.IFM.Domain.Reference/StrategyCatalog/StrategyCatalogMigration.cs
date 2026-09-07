using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.StrategyCatalog;

/// <summary>Idempotent default initialization with explicit legacy import. Never publishes or modifies Fund permissions.</summary>
public sealed class StrategyCatalogMigration(IDbContextFactory factory, IMarketDataApi marketData)
{
    public async Task<StrategyCatalogMigrationReport> EnsureAsync(CancellationToken ct = default, bool importLegacy = false)
    {
        var keys = new List<CatalogKey>();
        foreach (var example in StrategyCatalogDefaults.Create()) { await InsertMissing(example, ct); keys.Add(example.Key); }
        // Normal startup must not recreate legacy/test drafts removed from the catalog.
        var legacy = importLegacy ? await factory.ReferenceDb.GetTradeStrategyFamiliesAsync(ct) : [];
        foreach (var row in legacy.GroupBy(r => r.TradeStrategyFamilyId).Select(g => g.MaxBy(r => r.DefinitionVersion)!).Where(r => r.State == TradeStrategyFamilyState.Active))
        {
            var code = $"Legacy-{row.TradeStrategyFamilyId}-v{row.DefinitionVersion}";
            var key = new CatalogKey(StrategyCatalogKind.Deployment, StrategyCatalogExamples.StableId(code), 1);
            keys.Add(key);
            if (await factory.ConfigurationDb.GetStrategyCatalogAsync(key, ct) is not null) continue;
            var symbols = await marketData.GetTradeStrategySymbolsAsync(row.Family, ct);
            var matches = symbols.Success ? symbols.Value?.Where(x => x.Symbol == row.Symbol && x.Currency == row.Currency && (row.Exchange.Length == 0 || x.Exchange == row.Exchange)).ToArray() : null;
            var product = matches is { Length: 1 } ? matches[0] : null;
            var examples = StrategyCatalogDefaults.Create();
            var structureCode = row.Strategy switch { TradeStrategyType.Futures => "Future", TradeStrategyType.VerticalSpread => "CallVertical", TradeStrategyType.IronCondor => "IronCondor", _ => "" };
            var structures = row.Strategy == TradeStrategyType.VerticalSpread ? new[] { "CallVertical", "PutVertical" } : [structureCode];
            var structureKeys = examples.Where(x => x.Key.Kind == StrategyCatalogKind.Structure && structures.Contains(x.Code)).Select(x => x.Key).ToArray();
            var definition = StrategyCatalogExamples.New(StrategyCatalogKind.Deployment, code, row.Description[..Math.Min(row.Description.Length, 200)]) with
            {
                Parent = examples.Single(x => x.Key.Kind == StrategyCatalogKind.Strategy && x.Structures.Any(structureKeys.Contains)).Key, Horizon = row.TimeFrame,
                Variants = examples.Where(x => x.Key.Kind == StrategyCatalogKind.Variant && x.Parent is not null && structureKeys.Contains(x.Parent)).Select(x => x.Key).ToArray(),
                Products = product is null ? [] : [new(product.Id, product.Symbol, product.Exchange, product.Currency)],
                LegacyFamilies = [new(row.TradeStrategyFamilyId, row.DefinitionVersion)],
                Capabilities = [new("validator", "StrategyDeployment", 1)],
                Description = row.Description + "\n" + $"Imported from legacy family {row.TradeStrategyFamilyId} v{row.DefinitionVersion}. Draft only: review allowed variants, parameters and Fund permissions." + (product is null ? " Product metadata requires explicit resolution." : "")
            };
            await InsertMissing(definition, ct);
        }
        var definitions = new List<StoredStrategyCatalogDefinition>();
        foreach (var key in keys) definitions.Add(await factory.ConfigurationDb.GetStrategyCatalogAsync(key, ct) ?? throw new InvalidOperationException("Migration verification failed: missing exact catalog definition."));
        return new(definitions.Count(x => x.Definition.Key.Kind != StrategyCatalogKind.Deployment),
            definitions.Count(x => x.Definition.Key.Kind == StrategyCatalogKind.Deployment),
            definitions.Count(x => x.Definition.Key.Kind == StrategyCatalogKind.Deployment && x.Definition.Products.Length == 0));
    }

    async Task InsertMissing(StrategyCatalogDefinition definition, CancellationToken ct)
    {
        if (await factory.ConfigurationDb.GetStrategyCatalogAsync(definition.Key, ct) is not null) return;
        try { await factory.ConfigurationDb.InsertStrategyCatalogDraftAsync(definition, 0, "Legacy catalog migration", ct); }
        catch (InvalidOperationException)
        {
            if (await factory.ConfigurationDb.GetStrategyCatalogAsync(definition.Key, ct) is null) throw;
        }
    }
}

public sealed record StrategyCatalogMigrationReport(int StarterDefinitions, int ImportedDeployments, int DeploymentsRequiringProductResolution);
