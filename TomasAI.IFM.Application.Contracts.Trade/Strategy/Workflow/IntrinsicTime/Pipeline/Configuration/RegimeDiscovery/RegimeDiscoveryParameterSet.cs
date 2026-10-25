using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

/// <summary>Contains the complete immutable V1 configuration for one Regime Discovery execution.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryParameterSet
{
    /// <summary>Gets the current typed parameter schema version.</summary>
    public const ushort CurrentSchemaVersion = 1;
    /// <summary>Gets the immutable parameter-set identity.</summary>
    [Key(0)] public Guid ParameterSetId { get; init; }
    /// <summary>Gets the positive parameter-set version.</summary>
    [Key(1)] public int Version { get; init; }
    /// <summary>Gets the typed parameter schema version.</summary>
    [Key(2)] public ushort SchemaVersion { get; init; } = CurrentSchemaVersion;
    /// <summary>Gets the owning strategy parameter-set identity.</summary>
    [Key(3)] public Guid StrategyParameterSetId { get; init; }
    /// <summary>Gets the owning strategy parameter-set version.</summary>
    [Key(4)] public int StrategyParameterSetVersion { get; init; }
    /// <summary>Gets the single workflow target horizon.</summary>
    [Key(5)] public TimeFrameType TargetHorizon { get; init; }
    /// <summary>Gets the observation-timeframe mapping.</summary>
    [Key(6)] public RegimeDiscoveryHorizonConfiguration Horizon { get; init; } = new();
    /// <summary>Gets deterministic Trend parameters.</summary>
    [Key(7)] public TrendRegimeConfiguration Trend { get; init; } = new();
    /// <summary>Gets deterministic Volatility parameters.</summary>
    [Key(8)] public VolatilityRegimeConfiguration Volatility { get; init; } = new();
    /// <summary>Gets deterministic Market Structure parameters.</summary>
    [Key(9)] public MarketStructureRegimeConfiguration MarketStructure { get; init; } = new();
    /// <summary>Gets deterministic Fusion parameters.</summary>
    [Key(10)] public MarketRegimeFusionConfiguration Fusion { get; init; } = new();
    /// <summary>Gets signal freshness parameters.</summary>
    [Key(11)] public RegimeDiscoveryFreshnessConfiguration Freshness { get; init; } = new();
    /// <summary>Gets data-quality and compatibility parameters.</summary>
    [Key(12)] public RegimeDiscoveryDataQualityConfiguration DataQuality { get; init; } = new();

    /// <summary>Explicit signal list for schemas 2+; null retains the unchanged legacy schema-1 payload.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(13)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public RegimeDiscoverySignalConfiguration[]? SignalRequirements { get; init; }
    /// <summary>Gets normalized timeframe-and-period signal selections for schemas 5+.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(14)]
    [ParameterSchemaSince(5)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public RegimeDiscoverySignalMetricConfiguration[]? SignalMetrics { get; init; }

    /// <summary>Gets current market observation selections for schemas 5+.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(15)]
    [ParameterSchemaSince(5)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public RegimeDiscoveryObservationMetricConfiguration[]? ObservationMetrics { get; init; }

    /// <summary>Creates the approved V1 defaults for one target horizon.</summary>
    /// <param name="parameterSetId">Immutable Regime Discovery parameter identity.</param>
    /// <param name="strategyParameterSetId">Owning strategy parameter identity.</param>
    /// <param name="targetHorizon">Daily, Weekly, or Monthly target horizon.</param>
    /// <param name="version">Positive parameter-set version.</param>
    /// <param name="strategyVersion">Positive strategy parameter-set version.</param>
    /// <returns>A complete immutable default parameter set.</returns>
    public static RegimeDiscoveryParameterSet CreateDefault(
        Guid parameterSetId,
        Guid strategyParameterSetId,
        TimeFrameType targetHorizon,
        int version = 1,
        int strategyVersion = 1) => new()
        {
            ParameterSetId = parameterSetId,
            Version = version,
            StrategyParameterSetId = strategyParameterSetId,
            StrategyParameterSetVersion = strategyVersion,
            TargetHorizon = targetHorizon,
            Horizon = RegimeDiscoveryHorizonConfiguration.CreateDefault(targetHorizon)
        };
}
