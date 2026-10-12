using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

/// <summary>One complete immutable version. Relationships are stored in normalized tables, not in Settings.</summary>
public sealed record StrategyCatalogDefinition
{
    public required CatalogKey Key { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public short SchemaVersion { get; init; } = 1;
    // Variant -> Structure, ParameterSet -> ParameterSchema, Deployment -> Strategy.
    public CatalogKey? Parent { get; init; }
    public TimeFrameType Horizon { get; init; }
    public string Side { get; init; } = "";
    public string Bias { get; init; } = "";
    public string PremiumMode { get; init; } = "";
    public JsonElement Settings { get; init; } = JsonSerializer.SerializeToElement(new { });
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    public CatalogKey[] Families { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    public CatalogKey[] Structures { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    public CatalogKey[] Variants { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    public CatalogCapability[] Capabilities { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    public CatalogExpiryGroup[] ExpiryGroups { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    public CatalogLeg[] Legs { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    public CatalogVariantLeg[] VariantLegs { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    public CatalogProduct[] Products { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    public CatalogPipelineParameter[] PipelineParameters { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    public CatalogParameterBinding[] Parameters { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    public CatalogLegacyFamily[] LegacyFamilies { get; init; } = [];
}
