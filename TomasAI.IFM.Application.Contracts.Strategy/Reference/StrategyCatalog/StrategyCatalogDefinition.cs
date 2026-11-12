using MessagePack;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

/// <summary>One complete immutable version. Relationships are stored in normalized tables, not in Settings.</summary>
[MessagePackObject]
public sealed record StrategyCatalogDefinition
{
    [Key(0)]
    public required CatalogKey Key { get; init; }
    [Key(1)]
    public required string Code { get; init; }
    [Key(2)]
    public required string Name { get; init; }
    [Key(3)]
    public string Description { get; init; } = "";
    [Key(4)]
    public short SchemaVersion { get; init; } = 1;
    // Variant -> Structure, ParameterSet -> ParameterSchema, Deployment -> Strategy.
    [Key(5)]
    public CatalogKey? Parent { get; init; }
    [Key(6)]
    public TimeFrameType Horizon { get; init; }
    [Key(7)]
    public string Side { get; init; } = "";
    [Key(8)]
    public string Bias { get; init; } = "";
    [Key(9)]
    public string PremiumMode { get; init; } = "";
    [Key(10)]
    [MessagePackFormatter(typeof(CatalogSettingsMessagePackFormatter))]
    public JsonElement Settings { get; init; } = JsonSerializer.SerializeToElement(new { });
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    [Key(11)]
    public CatalogKey[] Families { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    [Key(12)]
    public CatalogKey[] Structures { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    [Key(13)]
    public CatalogKey[] Variants { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    [Key(14)]
    public CatalogCapability[] Capabilities { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    [Key(15)]
    public CatalogExpiryGroup[] ExpiryGroups { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    [Key(16)]
    public CatalogLeg[] Legs { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    [Key(17)]
    public CatalogVariantLeg[] VariantLegs { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    [Key(18)]
    public CatalogProduct[] Products { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    [Key(19)]
    public CatalogPipelineParameter[] PipelineParameters { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    [Key(20)]
    public CatalogParameterBinding[] Parameters { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
    [Key(21)]
    public CatalogLegacyFamily[] LegacyFamilies { get; init; } = [];
}
