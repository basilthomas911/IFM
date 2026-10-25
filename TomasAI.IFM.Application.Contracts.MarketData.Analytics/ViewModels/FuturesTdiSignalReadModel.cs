using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

/// <summary>
/// Represents a computed Traders Dynamic Index signal for a futures contract.
/// </summary>
/// <remarks>
/// MessagePack serializable. Only primitive/enumeration fields are serialized. Derived identifier
/// properties are excluded via <see cref="IgnoreMemberAttribute"/>. Follows the same pattern as
/// <c>FundOrderReadModel</c>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesTdiSignalReadModel
{
    /// <summary>Full futures contract identifier (root + contract month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Trading/value date for which this TDI signal applies.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    [Key(2)]
    public TimeFrameType TimePeriod { get; init; }

    /// <summary>Intraday timestamp (time component) when the signal was generated.</summary>
    [Key(3)]
    public TimeOnly Timestamp { get; init; }

    /// <summary>Consecutive up-trend count (coastline persistence metric).</summary>
    [Key(4)]
    public int UpTrendCount { get; init; }

    /// <summary>Consecutive down-trend count (coastline persistence metric).</summary>
    [Key(5)]
    public int DownTrendCount { get; init; }

    /// <summary>Computed trend direction (e.g., UpTrending, DownTrending, Reversal).</summary>
    [Key(6)]
    public FuturesTrendDirectionType TDI { get; init; }

    /// <summary>Strength of the computed trend direction (e.g., Low, Medium, High).</summary>
    [Key(7)]
    public FuturesTrendDirectionStrengthType TDIStrength { get; init; }

    /// <summary>Message/storage schema version. Version 2 is the Traders Dynamic Index contract.</summary>
    [Key(8)] public int SchemaVersion { get; init; }

    /// <summary>Stable identifier of the exact calculation configuration.</summary>
    [Key(9)] public string ConfigurationId { get; init; } = FuturesTdiConfiguration.StandardConfigurationId;

    [Key(10)] public int RsiPeriod { get; init; }
    [Key(11)] public int PriceLinePeriod { get; init; }
    [Key(12)] public int SignalLinePeriod { get; init; }
    [Key(13)] public int MarketBasePeriod { get; init; }
    [Key(14)] public int VolatilityBandPeriod { get; init; }
    [Key(15)] public double VolatilityBandDeviation { get; init; }
    [Key(16)] public decimal Price { get; init; }
    [Key(17)] public double Rsi { get; init; }
    [Key(18)] public double PriceLine { get; init; }
    [Key(19)] public double SignalLine { get; init; }
    [Key(20)] public double MarketBaseLine { get; init; }
    [Key(21)] public double UpperVolatilityBand { get; init; }
    [Key(22)] public double LowerVolatilityBand { get; init; }
    [Key(23)] public double BandWidth { get; init; }
    [Key(24)] public double PriceSignalDivergence { get; init; }
    [Key(25)] public FuturesTdiCrossType Cross { get; init; }
    [Key(26)] public FuturesTdiMarketStateType MarketState { get; init; }
    [Key(27)] public long SourceSequence { get; init; }
    [Key(28)] public DateTime SourceEventTimestamp { get; init; }

    /// <summary>
    /// Entity identifier consisting of contract id and value date (not serialized).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesTdiSignalEntityId EntityId => new(
        ContractId ?? string.Empty,
        ValueDate,
        TimePeriod,
        ConfigurationId);

    /// <summary>
    /// Full signal identifier including timestamp (not serialized).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesTdiSignalId Id => new(
        ContractId ?? string.Empty,
        ValueDate,
        TimePeriod,
        Timestamp,
        ConfigurationId);

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public FuturesTdiSignalReadModel() { }

    /// <summary>
    /// Full constructor initializing all serialized TDI signal properties.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date for the signal.</param>
    /// <param name="timePeriod">Time period for the signal.</param>
    /// <param name="timestamp">Intraday timestamp.</param>
    /// <param name="upTrendCount">Consecutive up-trend count.</param>
    /// <param name="downTrendCount">Consecutive down-trend count.</param>
    /// <param name="tdi">Computed trend direction.</param>
    /// <param name="tdiStrength">Strength of the trend direction.</param>
    public FuturesTdiSignalReadModel(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        TimeOnly timestamp,
        int upTrendCount,
        int downTrendCount,
        FuturesTrendDirectionType tdi,
        FuturesTrendDirectionStrengthType tdiStrength)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        Timestamp = timestamp;
        UpTrendCount = upTrendCount;
        DownTrendCount = downTrendCount;
        TDI = tdi;
        TDIStrength = tdiStrength;
    }

    /// <summary>Creates a schema-version-2 Traders Dynamic Index read model.</summary>
    public FuturesTdiSignalReadModel(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        TimeOnly timestamp,
        FuturesTdiConfiguration configuration,
        decimal price,
        double rsi,
        double priceLine,
        double signalLine,
        double marketBaseLine,
        double upperVolatilityBand,
        double lowerVolatilityBand,
        FuturesTrendDirectionType trendDirection,
        FuturesTrendDirectionStrengthType trendStrength,
        FuturesTdiCrossType cross,
        FuturesTdiMarketStateType marketState,
        long sourceSequence = 0,
        DateTime sourceEventTimestamp = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        Timestamp = timestamp;
        TDI = trendDirection;
        TDIStrength = trendStrength;
        SchemaVersion = FuturesTdiConfiguration.CurrentSchemaVersion;
        ConfigurationId = configuration.ConfigurationId;
        RsiPeriod = configuration.RsiPeriod;
        PriceLinePeriod = configuration.PriceLinePeriod;
        SignalLinePeriod = configuration.SignalLinePeriod;
        MarketBasePeriod = configuration.MarketBasePeriod;
        VolatilityBandPeriod = configuration.VolatilityBandPeriod;
        VolatilityBandDeviation = configuration.VolatilityBandDeviation;
        Price = price;
        Rsi = rsi;
        PriceLine = priceLine;
        SignalLine = signalLine;
        MarketBaseLine = marketBaseLine;
        UpperVolatilityBand = upperVolatilityBand;
        LowerVolatilityBand = lowerVolatilityBand;
        BandWidth = upperVolatilityBand - lowerVolatilityBand;
        PriceSignalDivergence = priceLine - signalLine;
        Cross = cross;
        MarketState = marketState;
        SourceSequence = sourceSequence;
        SourceEventTimestamp = sourceEventTimestamp;
    }

    /// <summary>
    /// Returns a compact JSON representation (for diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
