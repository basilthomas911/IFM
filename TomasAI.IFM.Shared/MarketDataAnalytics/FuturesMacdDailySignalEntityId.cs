using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketDataAnalytics;

/// <summary>
/// Represents a unique identifier for a MACD signal associated with a specific futures contract, including the contract
/// ID, value date, and time period type.
/// </summary>
/// <remarks>This record is designed for use with MessagePack serialization and provides methods for explicit
/// creation, stable string formatting, and compact JSON serialization. The identifier enables consistent referencing
/// and storage of MACD signal entities for futures contracts across analytics and data processing systems.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesMacdDailySignalEntityId : IActorEntityId
{
    /// <summary>Futures contract identifier (root + month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Value (trading) date for the MACD signal.</summary>
    [Key(1)]
    public TradeTimePeriodType TimePeriod {  get; init; }

    [Key(2)]
    public int PeriodLength { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesMacdDailySignalEntityId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesMacdDailySignalEntityId"/>.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="timePeriod">Time period type.</param>
    /// <param name="periodLength">Length of the time period.</param>
    public FuturesMacdDailySignalEntityId(string contractId,  TradeTimePeriodType timePeriod, int periodLength)
    {
        ContractId = contractId;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
    }

    /// <summary>
    /// Factory method for explicit creation.
    /// </summary>
    public static FuturesMacdDailySignalEntityId Create(string contractId, TradeTimePeriodType timePeriod, int periodLength) 
        => new(contractId, timePeriod, periodLength);

    /// <summary>
    /// Formats the identifier into a stable string key: ContractId.yyyyMMdd.TimePeriod.PeriodLength
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[80], $"{ContractId}.{TimePeriod}.{PeriodLength}");

    /// <summary>
    /// Returns a compact JSON representation.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}

