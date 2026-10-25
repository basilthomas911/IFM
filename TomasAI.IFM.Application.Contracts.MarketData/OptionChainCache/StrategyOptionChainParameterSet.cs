using System.Collections.Immutable;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;

/// <summary>Global versioned option-universe and construction settings; no fund overrides are implied.</summary>
[MessagePackObject]
public sealed record StrategyOptionChainParameterSet
{
    static readonly JsonSerializerOptions ReadOptions = new()
    { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };

    /// <summary>Stable identity of the global set.</summary>
    [Key(0)] public Guid ParameterSetId { get; init; }
    /// <summary>Immutable published policy version.</summary>
    [Key(1)] public int Version { get; init; } = 1;
    /// <summary>Business label.</summary>
    [Key(2)] public string Name { get; init; } = string.Empty;
    /// <summary>Exact strategy catalog structure identity.</summary>
    [Key(3)] public Guid StrategyDefinitionId { get; init; }
    /// <summary>Exact version of the referenced structure.</summary>
    [Key(4)] public int StrategyDefinitionVersion { get; init; }
    /// <summary>Canonical futures root, initially ES.</summary>
    [Key(5)] public string InstrumentRoot { get; init; } = "ES";
    /// <summary>Explicit environment; development parameters are not production defaults.</summary>
    [Key(6)] public string Environment { get; init; } = "Development";
    /// <summary>Whether background maintenance should prepare this strategy.</summary>
    [Key(7)] public bool Enabled { get; init; }
    /// <summary>All three explicitly configured directions.</summary>
    [Key(8)] public ImmutableArray<OptionStrategyBiasParameters> BiasRows { get; init; } = [];
    /// <summary>Volatility envelope multiplier: F * IV * sqrt(T) * multiplier.</summary>
    [Key(9)] public double CacheRangeMultiplier { get; init; } = 2;
    /// <summary>Hard subscription bound; incomplete coverage cannot be called ready.</summary>
    [Key(10)] public int MaximumContracts { get; init; } = 2048;
    /// <summary>Hard number of expirations maintained for this strategy.</summary>
    [Key(11)] public int MaximumExpirations { get; init; } = 3;
    /// <summary>Wire schema of the configuration.</summary>
    [Key(12)] public int SchemaVersion { get; init; } = 1;

    /// <summary>Validates complete global bias rows and finite bounded coverage before publication.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The existing model/command Validate method validates object properties, not method parameters. Preserve ArgumentException and property-specific diagnostic labels consumed by validation/error adapters.")]
    public void Validate()
    {
        if (SchemaVersion != 1 || ParameterSetId == Guid.Empty
            || Version < 1 || Enabled && (StrategyDefinitionId == Guid.Empty || StrategyDefinitionVersion < 1)
            || !Enabled && (StrategyDefinitionId == Guid.Empty ? StrategyDefinitionVersion != 0 : StrategyDefinitionVersion < 1) || string.IsNullOrWhiteSpace(Name)
            || (!string.Equals((InstrumentRoot),("ES"),StringComparison.Ordinal)) || Environment is not ("Development" or "Paper" or "Production")
            || !double.IsFinite(CacheRangeMultiplier) || CacheRangeMultiplier is < 1 or > 5
            || MaximumContracts is < 4 or > 2048 || MaximumExpirations is < 1 or > 16
            || BiasRows.IsDefault || BiasRows.Length != 3
            || BiasRows.Any(x => x is null)
            || BiasRows.Select(x => x.MarketBias).Distinct().Count() != 3)
            throw new ArgumentException("Invalid global strategy option-chain parameter set.");
        foreach (var row in BiasRows) row.Validate();
    }

    /// <summary>Serializes the complete validated global policy.</summary>
    public string Serialize() { Validate(); return JsonSerializer.Serialize(this); }
    /// <summary>Fingerprints exact policy inputs with SHA256.</summary>
    public string Hash() { Validate(); return Convert.ToHexStringLower(SHA256.HashData(MessagePackSerializer.Serialize(this,MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.None)))); }
    /// <summary>Reads and validates persisted policy JSON; unknown fields are rejected.</summary>
    public static StrategyOptionChainParameterSet Read(string json)
    {
        var result = JsonSerializer.Deserialize<StrategyOptionChainParameterSet>(json, ReadOptions)
            ?? throw new ArgumentException("Missing global chain policy.", nameof(json));
        result.Validate(); return result;
    }
}
