using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

/// <summary>Catalog-backed choices for the existing string-key Fund contracts.</summary>
static class TradeFamilyCatalogSelection
{
    public static TradeStrategyFamilyReadModel[] Active(IEnumerable<TradeStrategyFamilyReadModel>? catalog)
    {
        var rows = catalog?.ToArray() ?? [];
        if (rows.Any(x => x is null || x.Validate().Count != 0))
            throw new ArgumentException("The trade strategy family catalog contains invalid definitions.");
        if (rows.Select(TradeStrategyFamilyReference.From).Distinct().Count() != rows.Length)
            throw new ArgumentException("The trade strategy family catalog contains ambiguous definitions.");
        var active = rows.GroupBy(x => x.TradeStrategyFamilyId).Select(x => x.MaxBy(v => v.DefinitionVersion)!)
            .Where(x => x.State == TradeStrategyFamilyState.Active).ToArray();
        if (active.Select(TradeStrategyFamilyReference.From).Distinct().Count() != active.Length)
            throw new ArgumentException("The trade strategy family catalog contains ambiguous active definitions.");
        return active.OrderBy(x => x.SystemKey, StringComparer.Ordinal).ThenBy(x => x.Symbol, StringComparer.Ordinal).ThenBy(x => x.TradeStrategyFamilyId).ThenBy(x => x.DefinitionVersion).ToArray();
    }

    public sealed record Choice(string SystemKey, string Label, TradeStrategyFamilyReference? Reference = null)
    {
        public static Choice From(TradeStrategyFamilyReadModel row) =>
            new(row.SystemKey, $"{row.Description} [{row.Symbol} / {row.Exchange} / {row.Currency} / {row.TimeFrame}] (ID {row.TradeStrategyFamilyId} v{row.DefinitionVersion})", TradeStrategyFamilyReference.From(row));
        public override string ToString() => Label;
    }
}
