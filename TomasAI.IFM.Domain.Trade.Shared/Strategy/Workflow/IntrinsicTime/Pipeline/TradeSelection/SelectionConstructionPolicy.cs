using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
/// <summary>Construction constraints from the exact policy; version two pins a reviewed bounded market universe and version three pins a global cache policy.</summary>
public sealed record SelectionConstructionPolicy
{
    [JsonRequired] public short SchemaVersion { get; init; }
    [JsonRequired] public Guid ParameterSetId { get; init; }
    [JsonRequired] public int Version { get; init; }
    [JsonRequired] public int MaximumLegs { get; init; }
    [JsonRequired] public int MinimumDaysToExpiry { get; init; }
    [JsonRequired] public int MaximumDaysToExpiry { get; init; }
    [JsonRequired] public decimal MinimumWingWidth { get; init; }
    [JsonRequired] public decimal MaximumWingWidth { get; init; }
    [JsonRequired] public string DeltaUnits { get; init; } = string.Empty;
    [JsonRequired] public decimal MaximumDeltaTolerance { get; init; }
    [JsonPropertyName("marketData"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? MarketData { get; init; }
    /// <summary>Schema three pins an immutable global strategy cache policy instead of a dated provider universe.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SelectionOptionChainCachePolicy? OptionChainCache { get; init; }
    /// <summary>Exact structure-specific global policies for a deployment containing several option structures.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SelectionStructureOptionChainCachePolicy[]? OptionChainCachePolicies { get; init; }
    /// <summary>Resolves only the selected exact structure; never loads or substitutes a current policy.</summary>
    public SelectionOptionChainCachePolicy? CachePolicy(CatalogKey structure) => OptionChainCache
        ?? OptionChainCachePolicies?.SingleOrDefault(x => x.StructureId == structure.Id && x.StructureVersion == structure.Version)?.Policy;
    static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, Converters = { new TradeSelectionPolicy.CanonicalDecimalConverter() } };
    public static SelectionConstructionPolicy Read(string json)
    {
        TradeSelectionPolicy.CheckJson(json);
        var p = JsonSerializer.Deserialize<SelectionConstructionPolicy>(json, Options) ?? throw new ArgumentException("Missing construction policy."); p.Validate(); return p;
    }
    public string Serialize() { Validate(); return JsonSerializer.Serialize(this, Options); }
    public string Hash() => TradeSelectionPolicy.HashJson(Serialize());
    public void Validate() => TradeSelectionContracts.Require((SchemaVersion == 1 && MarketData is null && OptionChainCache is null && OptionChainCachePolicies is null
            || SchemaVersion == 2 && OptionChainCache is null && OptionChainCachePolicies is null && ValidMarketData()
            || SchemaVersion == 3 && MarketData is null && (
                OptionChainCache is { } cache && cache.IsValid() && OptionChainCachePolicies is null
                || OptionChainCache is null && OptionChainCachePolicies is { Length: > 0 and <= 8 } pins
                    && pins.All(x => x.StructureId != Guid.Empty && x.StructureVersion > 0 && x.Policy is { } policy && policy.IsValid())
                    && pins.Select(x => (x.StructureId, x.StructureVersion)).Distinct().Count() == pins.Length))
        && ParameterSetId != Guid.Empty && Version > 0 && MaximumLegs is >= 1 and <= 4
        && MinimumDaysToExpiry >= 1 && MaximumDaysToExpiry >= MinimumDaysToExpiry && MaximumDaysToExpiry <= 730
        && MinimumWingWidth >= 0 && MaximumWingWidth >= MinimumWingWidth && MaximumWingWidth <= 10000 && DeltaUnits == "UnderlyingEquivalent" && MaximumDeltaTolerance is >= 0 and <= 1,
        "TS.CONFIG.COMPOSITION_SCHEMA", "Invalid or unsupported construction constraints.");

    bool ValidMarketData()
    {
        if (MarketData is not { ValueKind: JsonValueKind.Object } data) return false;
        string[] required = ["SchemaVersion", "Dataset", "Root", "ValueDate", "IncludeOptions", "ScopeComplete", "MaturityDate", "Options", "Futures"];
        string[] optional = ["Calendar", "Publication", "Conversion"];
        if (required.Any(name => !data.TryGetProperty(name, out _))
            || data.EnumerateObject().Any(p => !required.Contains(p.Name, StringComparer.Ordinal) && !optional.Contains(p.Name, StringComparer.Ordinal))) return false;
        if (data.GetProperty("SchemaVersion").ValueKind != JsonValueKind.Number || !data.GetProperty("SchemaVersion").TryGetInt32(out var schema) || schema != 1
            || data.GetProperty("Dataset").ValueKind != JsonValueKind.String || data.GetProperty("Dataset").GetString() != "GLBX.MDP3"
            || data.GetProperty("Root").ValueKind != JsonValueKind.String || data.GetProperty("Root").GetString() != "ES"
            || data.GetProperty("ScopeComplete").ValueKind != JsonValueKind.True
            || data.GetProperty("IncludeOptions").ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || data.GetProperty("ValueDate").ValueKind != JsonValueKind.String
            || !DateOnly.TryParseExact(data.GetProperty("ValueDate").GetString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _)
            || data.GetProperty("MaturityDate").ValueKind != JsonValueKind.String) return false;
        var options = data.GetProperty("Options"); var futures = data.GetProperty("Futures");
        if (options.ValueKind != JsonValueKind.Array || futures.ValueKind != JsonValueKind.Array
            || options.GetArrayLength() > 512 || futures.GetArrayLength() > 16) return false;
        if (!data.GetProperty("IncludeOptions").GetBoolean()) return options.GetArrayLength() == 0 && futures.GetArrayLength() > 0;
        return futures.GetArrayLength() == 0
            && DateOnly.TryParseExact(data.GetProperty("MaturityDate").GetString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _)
            && optional.All(name => data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object);
    }
    public void ValidateCandidate(SelectionCatalogDefinitionSnapshot structure, SelectionCatalogDefinitionSnapshot variant)
    {
        TradeSelectionContracts.Require(structure.Legs.Length <= MaximumLegs, "TS.CONFIG.COMPOSITION_SCHEMA", "Composition policy cannot construct this leg count.");
        if (structure.Legs.Any(x => x.InstrumentClass == "FuturesOption"))
        {
            TradeSelectionContracts.Require(SchemaVersion != 3 || CachePolicy(structure.Key) is not null,
                "TS.CONFIG.COMPOSITION_SCHEMA", "Exact option structure cache policy is missing.");
            var settings = variant.SettingsJson;
            using var doc = JsonDocument.Parse(settings); var root = doc.RootElement;
            TradeSelectionContracts.Require(MinimumWingWidth > 0 && root.GetProperty("MinimumWingWidth").GetDecimal() >= MinimumWingWidth && root.GetProperty("MaximumWingWidth").GetDecimal() <= MaximumWingWidth
                && root.GetProperty("BalanceTolerance").GetDecimal() <= MaximumDeltaTolerance, "TS.CONFIG.COMPOSITION_SCHEMA", "Variant and composition constraints do not agree.");
        }
    }
}

/// <summary>Pinned global cache policy from the accepted construction policy. Hash is the normalized cache parameter payload hash.</summary>
public sealed record SelectionOptionChainCachePolicy
{
    [JsonRequired] public Guid ParameterSetId { get; init; }
    [JsonRequired] public int Version { get; init; }
    [JsonRequired] public string ConfigurationDigest { get; init; } = "";
    [JsonRequired] public int MaximumQuoteAgeMilliseconds { get; init; } = 1000;
    [JsonRequired] public int MaximumQuoteSkewMilliseconds { get; init; } = 250;
    /// <summary>Checks identity, hash syntax and explicit quality bounds without loading configuration.</summary>
    public bool IsValid() => ParameterSetId != Guid.Empty && Version > 0 && ConfigurationDigest.Length == 64
        && ConfigurationDigest.All(Uri.IsHexDigit) && MaximumQuoteAgeMilliseconds is >= 1 and <= 5000
        && MaximumQuoteSkewMilliseconds is >= 0 and <= 2000;
}

/// <summary>Immutable mapping from an exact catalog structure version to its global option-chain policy.</summary>
public sealed record SelectionStructureOptionChainCachePolicy
{
    [JsonRequired] public Guid StructureId { get; init; }
    [JsonRequired] public int StructureVersion { get; init; }
    [JsonRequired] public SelectionOptionChainCachePolicy Policy { get; init; } = new();
}
