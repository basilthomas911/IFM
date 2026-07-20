using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics;

/// <summary>
/// Represents the unique identifier for a futures trade signal, composed of a contract identifier and a value date.
/// </summary>
/// <remarks>
/// MessagePack-serializable using stable numeric keys:
///   Key(0) => <see cref="ContractId"/>
///   Key(1) => <see cref="ValueDate"/>
///   Key(2) => <see cref="TimePeriod"/>
///   Key(3) => <see cref="SequenceId"/>
/// Implements <see cref="IActorEntityId"/> with dot-separated formatting: <c>ContractId.ValueDate.TimePeriod.SequenceId</c>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesTradeSignalId : IActorEntityId
{
    [Key(0)] public string ContractId { get; init; } 
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public TradeTimePeriodType TimePeriod { get; init; }
    [Key(3)] public long SequenceId { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FuturesTradeSignalId"/> record.
    /// </summary>
    /// <param name="contractId">The futures contract identifier.</param>
    /// <param name="valueDate">The value date of the signal.</param>
    /// <param name="timePeriod">The time period of the signal.</param>
    /// <param name="sequenceId">The sequence ID of the signal.</param>
    public FuturesTradeSignalId(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, long sequenceId)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        SequenceId = sequenceId;
    }

    /// <summary>
    /// Formats the identifier as a dot-separated string: <c>ContractId.ValueDate.TimePeriod.SequenceId</c> (date in yyyyMMdd).
    /// </summary>
    /// <returns>A dot-separated string representation.</returns>
    public string Format() => string.Create(null, stackalloc char[96], $"{ContractId}.{ValueDate:yyyyMMdd}.{TimePeriod}.{SequenceId}");

    /// <summary>
    /// Returns a JSON string representation of this identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    [IgnoreMember]
    public bool IsValid
        => !string.IsNullOrEmpty(ContractId) && ValueDate > DateOnly.MinValue && SequenceId > 0;
}
