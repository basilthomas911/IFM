using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>
/// Represents a unique identifier for a MACD signal associated with a specific futures contract, including the contract
/// ID, value date, and time period type.
/// </summary>
/// <remarks>This record is designed for use with MessagePack serialization and provides methods for explicit
/// creation, stable string formatting, and compact JSON serialization. The identifier enables consistent referencing
/// and storage of MACD signal entities for futures contracts across analytics and data processing systems.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesMacdSignalEntityId : IActorEntityId
{
    /// <summary>Futures contract identifier (root + month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Value (trading) date for the MACD signal.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    [Key(2)]
    public TimeFrameType TimePeriod {  get; init; }

    [Key(3)]
    public int SignalEmaPeriod { get; init; } = FuturesMacdConfiguration.ConventionalSignalEmaPeriod;

    [Key(4)]
    public int FastEmaPeriod { get; init; } = FuturesMacdConfiguration.ConventionalFastEmaPeriod;

    [Key(5)]
    public int SlowEmaPeriod { get; init; } = FuturesMacdConfiguration.ConventionalSlowEmaPeriod;

    [IgnoreMember]
    [Obsolete("Use SignalEmaPeriod, FastEmaPeriod, and SlowEmaPeriod.")]
    public int PeriodLength => SignalEmaPeriod;

    [IgnoreMember]
    public FuturesMacdConfiguration Configuration
        => new(SignalEmaPeriod, FastEmaPeriod, SlowEmaPeriod);

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesMacdSignalEntityId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesMacdSignalEntityId"/>.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date.</param>
    /// <param name="timePeriod">Time period type.</param>
    /// <param name="signalEmaPeriod">Signal-line EMA period.</param>
    /// <param name="fastEmaPeriod">Fast EMA period.</param>
    /// <param name="slowEmaPeriod">Slow EMA period.</param>
    public FuturesMacdSignalEntityId(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int signalEmaPeriod = FuturesMacdConfiguration.ConventionalSignalEmaPeriod,
        int fastEmaPeriod = FuturesMacdConfiguration.ConventionalFastEmaPeriod,
        int slowEmaPeriod = FuturesMacdConfiguration.ConventionalSlowEmaPeriod)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        SignalEmaPeriod = signalEmaPeriod;
        FastEmaPeriod = fastEmaPeriod;
        SlowEmaPeriod = slowEmaPeriod;
    }

    /// <summary>
    /// Factory method for explicit creation.
    /// </summary>
    public static FuturesMacdSignalEntityId Create(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int signalEmaPeriod = FuturesMacdConfiguration.ConventionalSignalEmaPeriod,
        int fastEmaPeriod = FuturesMacdConfiguration.ConventionalFastEmaPeriod,
        int slowEmaPeriod = FuturesMacdConfiguration.ConventionalSlowEmaPeriod)
        => new(contractId, valueDate, timePeriod, signalEmaPeriod, fastEmaPeriod, slowEmaPeriod);

    /// <summary>
    /// Formats the identifier into a stable string key containing all three EMA periods.
    /// </summary>
    public string Format() => string.Create(
        null,
        stackalloc char[112],
        $"{ContractId}.{ValueDate:yyyyMMdd}.{TimePeriod}.{SignalEmaPeriod}.{FastEmaPeriod}.{SlowEmaPeriod}");

    /// <summary>
    /// Returns a compact JSON representation.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
