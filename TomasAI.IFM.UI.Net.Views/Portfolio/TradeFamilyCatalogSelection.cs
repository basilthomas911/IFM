using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
namespace TomasAI.IFM.UI.Net.Views.Portfolio;

static class TradeFamilyCatalogSelection
{
    public static StrategyDeploymentChoice[] Active(IEnumerable<StrategyDeploymentChoice>? catalog)
    {
        var rows = catalog?.ToArray() ?? [];
        if (rows.Any(x => x.Key.Kind != StrategyCatalogKind.Deployment || x.Key.Id == Guid.Empty || x.Key.Version <= 0) || rows.Select(x => x.Key).Distinct().Count() != rows.Length)
            throw new ArgumentException("Invalid or duplicate ConfigurationDb deployment identities.");
        return rows.GroupBy(x => x.Key.Id).Select(g => g.MaxBy(x => x.Key.Version)!).Where(x => x.Status != CatalogLifecycleStatus.Retired).OrderBy(x => x.Code, StringComparer.Ordinal).ToArray();
    }
    public sealed record Choice(string SystemKey, string Label, TradeStrategyFamilyReference? Reference = null, StrategyDeploymentChoice? Deployment = null)
    {
        public static Choice From(StrategyDeploymentChoice row) => new(row.Code,
            $"{row.Name} [{row.Symbol} / {row.Currency} / {row.TimeFrame} {row.Exchange}] v{row.Key.Version} {row.Status}", row.Reference, row);
        public override string ToString() => Label;
    }
}
