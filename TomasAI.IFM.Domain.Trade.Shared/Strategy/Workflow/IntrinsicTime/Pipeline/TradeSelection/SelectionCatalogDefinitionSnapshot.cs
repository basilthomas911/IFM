using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

[MessagePackObject]
public sealed record SelectionCatalogDefinitionSnapshot
{
    [Key(0)] public short SchemaVersion { get; init; }
    [Key(1)] public CatalogKey Key { get; init; }
    [Key(2)] public string Code { get; init; } = string.Empty;
    [Key(3)] public string Name { get; init; } = string.Empty;
    [Key(4)] public string Description { get; init; } = string.Empty;
    [Key(5)] public short DefinitionSchemaVersion { get; init; }
    [Key(6)] public CatalogKey? Parent { get; init; }
    [Key(7)] public TimeFrameType Horizon { get; init; }
    [Key(8)] public string Side { get; init; } = string.Empty;
    [Key(9)] public string Bias { get; init; } = string.Empty;
    [Key(10)] public string PremiumMode { get; init; } = string.Empty;
    [Key(11)] public string SettingsJson { get; init; } = string.Empty;
    CatalogKey[] _Families = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(12)] public CatalogKey[] Families { get => [.. _Families]; init => _Families = value is null ? [] : [.. value]; }
    CatalogKey[] _Structures = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(13)] public CatalogKey[] Structures { get => [.. _Structures]; init => _Structures = value is null ? [] : [.. value]; }
    CatalogKey[] _Variants = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(14)] public CatalogKey[] Variants { get => [.. _Variants]; init => _Variants = value is null ? [] : [.. value]; }
    SelectionCapability[] _Capabilities = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(15)] public SelectionCapability[] Capabilities { get => [.. _Capabilities]; init => _Capabilities = value is null ? [] : [.. value]; }
    SelectionExpiryGroup[] _ExpiryGroups = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(16)] public SelectionExpiryGroup[] ExpiryGroups { get => [.. _ExpiryGroups]; init => _ExpiryGroups = value is null ? [] : [.. value]; }
    SelectionLeg[] _Legs = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(17)] public SelectionLeg[] Legs { get => [.. _Legs]; init => _Legs = value is null ? [] : [.. value]; }
    SelectionVariantLeg[] _VariantLegs = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(18)] public SelectionVariantLeg[] VariantLegs { get => [.. _VariantLegs]; init => _VariantLegs = value is null ? [] : [.. value]; }
    SelectionProduct[] _Products = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(19)] public SelectionProduct[] Products { get => [.. _Products]; init => _Products = value is null ? [] : [.. value]; }
    SelectionPipelineParameter[] _PipelineParameters = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(20)] public SelectionPipelineParameter[] PipelineParameters { get => [.. _PipelineParameters]; init => _PipelineParameters = value is null ? [] : [.. value]; }
    SelectionParameterBinding[] _Parameters = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(21)] public SelectionParameterBinding[] Parameters { get => [.. _Parameters]; init => _Parameters = value is null ? [] : [.. value]; }
    SelectionLegacyFamily[] _LegacyFamilies = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(22)] public SelectionLegacyFamily[] LegacyFamilies { get => [.. _LegacyFamilies]; init => _LegacyFamilies = value is null ? [] : [.. value]; }
    [Key(23)] public string ContentHash { get; init; } = string.Empty;
    [Key(24)] public CatalogLifecycleStatus Status { get; init; }
    [Key(25)] public DateTime CreatedUtc { get; init; }
    [Key(26)] public string CreatedBy { get; init; } = string.Empty;
    [Key(27)] public DateTime? EffectiveFromUtc { get; init; }
    [Key(28)] public string? PublishedBy { get; init; }
    [Key(29)] public DateTime? RetiredAtUtc { get; init; }
    [Key(30)] public string? RetiredBy { get; init; }
}
