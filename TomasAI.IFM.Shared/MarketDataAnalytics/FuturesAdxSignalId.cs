using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketDataAnalytics;

/// <summary>
/// Represents a unique identifier for a futures ADX signal, encapsulating the contract ID, value date, time period, and
/// timestamp associated with the signal.
/// </summary>
/// <remarks>This record is used to distinguish individual ADX signals for futures contracts. It provides
/// essential information for analyzing market conditions and contract performance, and is compatible with serialization
/// frameworks such as MessagePack. The identifier can be formatted as a stable string key for storage or lookup
/// scenarios.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesAdxSignalId : IActorEntityId
{
    /// <summary>Futures contract identifier (root + contract month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Value date associated with the ADX signal.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    [Key(2)]
    public TradeTimePeriodType TimePeriod { get; init; }

    [Key(3)]
    public int PeriodLength { get; init; }

    /// <summary>Timestamp (intraday time component) linked to the signal generation.</summary>
    [Key(4)]
    public TimeOnly Timestamp { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesAdxSignalId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesAdxSignalId"/>.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date of the signal.</param>
    /// <param name="timePeriod">Futures time period type.</param>
    /// <param name="periodLength">The length of the time period.</param>
    /// <param name="timestamp">Intraday timestamp component.</param>
    public FuturesAdxSignalId(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength, TimeOnly timestamp)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Factory method for creating a new identifier instance.
    /// </summary>
    public static FuturesAdxSignalId Create(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength, TimeOnly timestamp)
        => new(contractId, valueDate, timePeriod, periodLength, timestamp);

    /// <summary>
    /// Formats the identifier into a stable string key: ContractId.yyyyMMdd.Timestamp
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[100], $"{ContractId}.{ValueDate:yyyyMMdd}.{TimePeriod}.{PeriodLength}.{Timestamp:HHmmss}");

    /// <summary>
    /// Returns a compact JSON representation.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    public FuturesAdxSignalEntityId ToEntityId() => new(ContractId, ValueDate, TimePeriod, PeriodLength);
    public FuturesAdxDailySignalEntityId ToDailyEntityId() => new(ContractId, TimePeriod, PeriodLength);
}

public static class FuturesAdxSignalIdExtensions
{
    /// <summary>
    /// Validates the <see cref="FuturesAdxSignalId"/> object and adds any validation errors to the provided list.
    /// </summary>
    /// <remarks>
    /// This method checks that the <c>ContractId</c> is not empty, the <c>ValueDate</c> is within valid range,
    /// and the <c>Timestamp</c> is within valid range. If any property is invalid, a corresponding <see
    /// cref="ValidationError"/> is added to the <paramref name="validationErrors"/> list with a message prefixed by the
    /// <paramref name="commandName"/>.
    /// </remarks>
    /// <param name="validationErrors">A list of <see cref="ValidationError"/> objects to which any validation errors will be added.</param>
    /// <param name="futuresAdxSignalId">The <see cref="FuturesAdxSignalId"/> object to validate. The <c>ContractId</c> must not be empty,
    /// <c>ValueDate</c> must be within valid range, and <c>Timestamp</c> must be within valid range.</param>
    /// <param name="commandName">The name of the command being validated, used to prefix error messages for context.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects, including any new validation errors found.</returns>
    public static List<ValidationError> ValidateFuturesAdxSignalId(this List<ValidationError> validationErrors, FuturesAdxSignalId futuresAdxSignalId, string commandName)
    {
        if (string.IsNullOrEmpty(futuresAdxSignalId.ContractId))
            validationErrors.Add(new ($"{commandName}.ContractId is required"));
        if (futuresAdxSignalId.ValueDate == DateOnly.MinValue)
            validationErrors.Add(new ($"{commandName}.ValueDate must be greater than DateOnly.MinValue"));
        if (futuresAdxSignalId.ValueDate == DateOnly.MaxValue)
            validationErrors.Add(new ($"{commandName}.ValueDate must be less than DateOnly.MaxValue"));
        if (futuresAdxSignalId.TimePeriod == TradeTimePeriodType.None)
            validationErrors.Add(new ($"{commandName}.TimePeriod is invalid"));
        if (futuresAdxSignalId.PeriodLength <= 0)
            validationErrors.Add(new ($"{commandName}.PeriodLength must be a positive integer   "));
        if (futuresAdxSignalId.Timestamp == TimeOnly.MinValue)
            validationErrors.Add(new ($"{commandName}.Timestamp must be greater than TimeOnly.MinValue"));
        if (futuresAdxSignalId.Timestamp == TimeOnly.MaxValue)
            validationErrors.Add(new ($"{commandName}.Timestamp must be less than TimeOnly.MaxValue"));
        return validationErrors;
    }
}

