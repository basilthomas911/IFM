using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

// These are persistence entity kinds, not a closed list of trading strategies.
public enum StrategyCatalogKind : short
{
    Family = 1, Strategy = 2, Structure = 3, Variant = 4,
    ParameterSchema = 5, ParameterSet = 6, Deployment = 7
}

[MessagePack.MessagePackObject]
public sealed record CatalogKey([property: MessagePack.Key(0)] StrategyCatalogKind Kind, [property: MessagePack.Key(1)] Guid Id, [property: MessagePack.Key(2)] int Version);
public sealed record CatalogCapability(string Role, string Code, int Version);
public sealed record CatalogExpiryGroup(string Key, string? AfterGroup = null);
public sealed record CatalogLeg(string Key, string InstrumentClass, string Side, string OptionRight,
    decimal Ratio, string ExpiryGroup);
public sealed record CatalogVariantLeg(string LegKey, string Side, decimal Ratio);
public sealed record CatalogProduct(int ProductId, string Symbol, string Exchange, string Currency);
public sealed record CatalogPipelineParameter(string Role, CatalogPipelineParameterKind Kind, Guid Id, int Version, string Hash);
public sealed record CatalogParameterBinding(string Role, CatalogKey ParameterSet);
public sealed record CatalogLegacyFamily(int Id, long Version);

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
    public CatalogKey[] Families { get; init; } = [];
    public CatalogKey[] Structures { get; init; } = [];
    public CatalogKey[] Variants { get; init; } = [];
    public CatalogCapability[] Capabilities { get; init; } = [];
    public CatalogExpiryGroup[] ExpiryGroups { get; init; } = [];
    public CatalogLeg[] Legs { get; init; } = [];
    public CatalogVariantLeg[] VariantLegs { get; init; } = [];
    public CatalogProduct[] Products { get; init; } = [];
    public CatalogPipelineParameter[] PipelineParameters { get; init; } = [];
    public CatalogParameterBinding[] Parameters { get; init; } = [];
    public CatalogLegacyFamily[] LegacyFamilies { get; init; } = [];
}

public sealed record StoredStrategyCatalogDefinition(StrategyCatalogDefinition Definition, string ContentHash,
    CatalogLifecycleStatus Status, DateTime CreatedUtc, string CreatedBy,
    DateTime? EffectiveFromUtc, string? PublishedBy, DateTime? RetiredAtUtc, string? RetiredBy);
public sealed record StrategyCatalogSummary(CatalogKey Key, string Code, string Name,
    CatalogLifecycleStatus Status, string ContentHash);

/// <summary>Catalog evidence only. It neither authorizes a Fund nor activates a workflow.</summary>
public sealed record StrategyCatalogSnapshot(CatalogKey Deployment, DateTime AsOfUtc,
    StoredStrategyCatalogDefinition[] Definitions, string ContentHash);

/// <summary>Trusted server-side capability registry. Implementations validate semantics as well as availability.</summary>
public interface IStrategyCatalogCapabilities
{
    void Validate(CatalogCapability capability, StrategyCatalogDefinition owner,
        IReadOnlyDictionary<CatalogKey, StoredStrategyCatalogDefinition> dependencies);
}

/// <summary>Exact external catalog validation, supplied by the owning domain adapter; never a caller approval flag.</summary>
public interface IStrategyCatalogReferences
{
    Task ValidateProductAsync(CatalogProduct product, CancellationToken cancellationToken);
    Task ValidateLegacyFamilyAsync(CatalogLegacyFamily family, StrategyCatalogDefinition deployment,
        CancellationToken cancellationToken);
}

public enum CatalogValueType { Object = 1, Array = 2, String = 3, Decimal = 4, Integer = 5, Boolean = 6 }

/// <summary>Bounded parameter-shape DSL; not an implementation of general-purpose JSON Schema.</summary>
public sealed record CatalogParameterShape
{
    public CatalogValueType Type { get; init; } = CatalogValueType.Object;
    public Dictionary<string, CatalogParameterShape> Properties { get; init; } = new(StringComparer.Ordinal);
    public string[] Required { get; init; } = [];
    public CatalogParameterShape? Items { get; init; }
    public decimal? Minimum { get; init; }
    public decimal? Maximum { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public string[] Choices { get; init; } = [];
    public string Unit { get; init; } = "";
}

public enum CatalogLifecycleStatus : short { Draft = 0, Published = 1, Retired = 2 }
public enum CatalogPipelineParameterKind : short { IntrinsicTimeStrategyWorkflow = 1, RegimeDiscovery = 2, MarketCondition = 3, TradeSelection = 4, OrderComposition = 5, RiskManagement = 6, MarketConditionAssessment = 7 }
