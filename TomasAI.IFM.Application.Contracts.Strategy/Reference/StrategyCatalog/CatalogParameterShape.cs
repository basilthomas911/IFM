using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

/// <summary>Bounded parameter-shape DSL; not an implementation of general-purpose JSON Schema.</summary>
public sealed record CatalogParameterShape
{
    public CatalogValueType Type { get; init; } = CatalogValueType.Object;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing serialized concrete model collection contract.")]
    public Dictionary<string, CatalogParameterShape> Properties { get; init; } = new(StringComparer.Ordinal);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    public string[] Required { get; init; } = [];
    public CatalogParameterShape? Items { get; init; }
    public decimal? Minimum { get; init; }
    public decimal? Maximum { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    public string[] Choices { get; init; } = [];
    public string Unit { get; init; } = "";
}
