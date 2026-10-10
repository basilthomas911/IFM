using MessagePack;
using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Risk;

/// <summary>Versioned strategy position-monitoring policy. Currency comes from the four broker pricing contracts, not a fixed currency setting.</summary>
[MessagePackObject]
public sealed record StrategyRiskParameterSet
{
    /// <summary>Gets the message and JSON schema version.</summary>
    [Key(0), JsonRequired] public int SchemaVersion { get; init; } = 1;
    /// <summary>Gets the stable identity shared by all versions of this parameter set.</summary>
    [Key(1), JsonRequired] public Guid ParameterSetId { get; init; }
    /// <summary>Gets the immutable policy version; changing any parameter requires a new version.</summary>
    [Key(2), JsonRequired] public int Version { get; init; } = 1;
    /// <summary>Gets the business name displayed when selecting a parameter set.</summary>
    [Key(3), JsonRequired] public string Name { get; init; } = string.Empty;
    /// <summary>Gets the strategy to which this set applies.</summary>
    [Key(4), JsonRequired] public TradeStrategyKind StrategyKind { get; init; }
    /// <summary>Gets the exact underlying root to which the point-move setting applies.</summary>
    [Key(5), JsonRequired] public string InstrumentRoot { get; init; } = string.Empty;
    /// <summary>Gets Development, Paper or Production; a development set is not a production default.</summary>
    [Key(6), JsonRequired] public string Environment { get; init; } = string.Empty;
    /// <summary>Gets the first supported strategy-specific daily loss and scenario policy.</summary>
    [Key(7), JsonRequired] public IronCondorRiskParameters? IronCondor { get; init; }

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new TradeSelectionPolicy.CanonicalDecimalConverter() }
    };

    /// <summary>Validates the supported strategy family and all parameter units and bounds.</summary>
    public void Validate()
    {
        if (SchemaVersion != 1 || ParameterSetId == Guid.Empty || Version < 1 || string.IsNullOrWhiteSpace(Name)
            || string.IsNullOrWhiteSpace(InstrumentRoot) || Environment is not ("Development" or "Paper" or "Production")
            || StrategyKind != TradeStrategyKind.IronCondor || IronCondor is null)
            throw new ArgumentException("StrategyRiskParameterSet.INVALID");
        IronCondor.Validate();
    }

    /// <summary>Serializes the exact validated policy for persistence.</summary>
    public string Serialize() { Validate(); return JsonSerializer.Serialize(this, JsonOptions); }

    /// <summary>Calculates the canonical SHA-256 of the complete versioned policy.</summary>
    public string Hash() => TradeSelectionPolicy.HashJson(Serialize());

    /// <summary>Reads strict policy JSON; unknown or duplicate properties and invalid values are rejected.</summary>
    public static StrategyRiskParameterSet Read(string json)
    {
        TradeSelectionPolicy.CheckJson(json);
        var result = JsonSerializer.Deserialize<StrategyRiskParameterSet>(json, JsonOptions)
            ?? throw new ArgumentException("StrategyRiskParameterSet.MISSING");
        result.Validate(); return result;
    }

    /// <summary>Creates the explicit ES development policy: five minutes, both ten-point futures moves, unchanged IV and 1,000 broker-currency daily loss.</summary>
    /// <remarks>Creation does not publish, assign or activate the policy for any trade or production environment.</remarks>
    public static StrategyRiskParameterSet CreateIronCondorDevelopmentDefault() => new()
    {
        ParameterSetId = Guid.Parse("1bdb3da4-4cab-4db1-a90e-f8d0e2a7a401"),
        Name = "Iron Condor ES Development", StrategyKind = TradeStrategyKind.IronCondor,
        InstrumentRoot = "ES", Environment = "Development", IronCondor = new()
    };
}

/// <summary>Explicit daily-loss monitoring settings. Monetary parameters use the common broker option contract currency.</summary>
[MessagePackObject]
public sealed record IronCondorRiskParameters
{
    /// <summary>Gets the positive daily loss threshold in broker currency.</summary>
    [Key(0), JsonRequired] public decimal DailyLossLimit { get; init; } = 1000m;
    /// <summary>Gets the future valuation horizon in seconds; 300 means five minutes.</summary>
    [Key(1), JsonRequired] public int ScenarioHorizonSeconds { get; init; } = 300;
    /// <summary>Gets the absolute underlying move in points; evaluate both plus and minus this value.</summary>
    [Key(2), JsonRequired] public decimal AdverseFuturesMovePoints { get; init; } = 10m;
    /// <summary>Gets the additive IV shift in percentage points; zero leaves IV unchanged.</summary>
    [Key(3), JsonRequired] public double ScenarioVolatilityShiftPercentagePoints { get; init; }
    /// <summary>Gets the ratio at which a negative daily PnL produces an exit recommendation.</summary>
    [Key(4), JsonRequired] public double ExitForwardLossRatio { get; init; } = 1;
    /// <summary>Gets the earlier warning ratio; warnings do not execute an exit.</summary>
    [Key(5), JsonRequired] public double WarningForwardLossRatio { get; init; } = .8;
    /// <summary>Gets the maximum allowed age of source quotes in seconds.</summary>
    [Key(6), JsonRequired] public int MaximumQuoteAgeSeconds { get; init; } = 30;
    /// <summary>Gets the provisional additional commission per closing option contract, in broker currency.</summary>
    [Key(7), JsonRequired] public decimal ExitCommissionPerContract { get; init; } = 5m;
    /// <summary>Gets additional slippage ticks per leg beyond the captured closing-price basis; zero means no extra tick allowance.</summary>
    [Key(8), JsonRequired] public int AdditionalExitSlippageTicksPerLeg { get; init; } = 1;

    /// <summary>Rejects missing, nonfinite or inconsistent daily-loss configuration.</summary>
    public void Validate()
    {
        if (DailyLossLimit <= 0 || ScenarioHorizonSeconds <= 0 || AdverseFuturesMovePoints <= 0
            || !double.IsFinite(ScenarioVolatilityShiftPercentagePoints)
            || !double.IsFinite(ExitForwardLossRatio) || ExitForwardLossRatio <= 0
            || !double.IsFinite(WarningForwardLossRatio) || WarningForwardLossRatio <= 0
            || WarningForwardLossRatio >= ExitForwardLossRatio || MaximumQuoteAgeSeconds <= 0
            || ExitCommissionPerContract < 0 || AdditionalExitSlippageTicksPerLeg < 0)
            throw new ArgumentException("IronCondorRiskParameters.INVALID");
    }
}
