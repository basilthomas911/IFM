using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.Lookups;

/// <summary>Values for Fund authoring, from ConfigurationDb and the published Databento product index.</summary>
public sealed record FundSelectionCatalog(string[] Underlyings, LookupDefinitionReadModel[] AssetTypes,
    LookupDefinitionReadModel[] Directions, LookupDefinitionReadModel[] MarketConditions)
{
    public static async Task<FundSelectionCatalog> LoadAsync(IReferenceQueryApi queries, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var assets = queries.GetLookupDefinitionsAsync(LookupDefinitionGroups.AssetTypes, ct);
        var directions = queries.GetLookupDefinitionsAsync(LookupDefinitionGroups.Directions, ct);
        var conditions = queries.GetLookupDefinitionsAsync(LookupDefinitionGroups.MarketConditions, ct);
        var futures = queries.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.Futures, ct);
        var options = queries.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.FuturesOption, ct);
        await Task.WhenAll(assets, directions, conditions, futures, options).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        var products = Required(await futures, "Futures underlyings").Concat(Required(await options, "Futures option underlyings")).ToArray();
        if (products.Any(x => x.Validate().Count != 0)) throw new InvalidOperationException("Invalid Databento product metadata.");
        var symbols = products.Select(x => x.Symbol).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (symbols.Length == 0) throw new InvalidOperationException("No current Databento underlying symbols are loaded. Refresh instrument definitions first.");
        return new(symbols, Group(await assets, LookupDefinitionGroups.AssetTypes), Group(await directions, LookupDefinitionGroups.Directions),
            Group(await conditions, LookupDefinitionGroups.MarketConditions));
    }

    static T[] Required<T>(ServiceResult<T[]> result, string name)
        => result.Success && result.Value is not null ? result.Value : throw new InvalidOperationException($"{name}: {result.ErrorMessage ?? "lookup unavailable"}");

    static LookupDefinitionReadModel[] Group(ServiceResult<LookupDefinitionReadModel[]> result, string group)
    {
        var rows = Required(result, group);
        if (rows.Length == 0 || rows.Any(x => x.Id <= 0 || x.GroupName != group || string.IsNullOrWhiteSpace(x.InternalValue) || string.IsNullOrWhiteSpace(x.DisplayName))
            || rows.Select(x => x.InternalValue).Distinct(StringComparer.Ordinal).Count() != rows.Length)
            throw new InvalidOperationException($"{group}: lookup definitions are missing or invalid.");
        return rows.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).ToArray();
    }

    public static bool IsSelectable(LookupDefinitionReadModel row) => row.IsEnabled && (row.GroupName switch
    {
        LookupDefinitionGroups.AssetTypes => row.InternalValue is "Futures" or "FuturesOption",
        LookupDefinitionGroups.Directions => Enum.TryParse<MarketConditionDirection>(row.InternalValue, out var direction)
            && Enum.IsDefined(direction) && direction != MarketConditionDirection.Undefined && direction.ToString() == row.InternalValue,
        LookupDefinitionGroups.MarketConditions => Enum.TryParse<MarketConditionType>(row.InternalValue, out var condition)
            && Enum.IsDefined(condition) && condition != MarketConditionType.Undefined && condition.ToString() == row.InternalValue,
        _ => false
    });

    public void ValidateSelections(string[] underlyings, string[] assets, string[] directions, string[] conditions)
    {
        Validate(underlyings, Underlyings, "Underlyings");
        Validate(assets, AssetTypes.Where(IsSelectable).Select(x => x.InternalValue), "Asset Types");
        Validate(directions, Directions.Where(IsSelectable).Select(x => x.InternalValue), "Directions");
        Validate(conditions, MarketConditions.Where(IsSelectable).Select(x => x.InternalValue), "Market Conditions");
    }

    static void Validate(string[] values, IEnumerable<string> available, string label)
    {
        var allowed = available.ToHashSet(StringComparer.Ordinal);
        if (values is null || values.Distinct(StringComparer.Ordinal).Count() != values.Length || values.Any(x => !allowed.Contains(x)))
            throw new ArgumentException($"{label} contains unavailable or duplicate selections. Select values from the current list.");
    }
}
