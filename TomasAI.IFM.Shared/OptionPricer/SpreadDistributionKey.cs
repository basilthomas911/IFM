using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer;

/// <summary>
/// Represents a unique identifier for a trade, encapsulating its trade ID, status, value date, and days to expiry.
/// </summary>
/// <remarks>This record is used to uniquely identify trade-related entities and operations. It enforces
/// validation to ensure that the trade ID is positive, the value date is valid, and the days to expiry is non-negative.
/// Instances of this type are suitable for use as keys in collections or for message passing scenarios where a
/// composite trade identifier is required.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record SpreadDistributionKey : IActorEntityId
{
    [Key(0)] public int TradeId { get; init; }
    [Key(1)] public TradeStatus TradeStatus { get; init; }
    [Key(2)] public DateOnly ValueDate { get; init; }
    [Key(3)] public int DaysToExpiry { get; init; }

    /// <summary>
    /// Parameterless constructor required by some serializers; initializes to defaults.
    /// </summary>
    public SpreadDistributionKey()  { }

    public SpreadDistributionKey(
        int tradeId,
        TradeStatus tradeStatus,
        DateOnly valueDate,
         int daysToExpiry)
    {
        if (tradeId < 1) throw new ArgumentOutOfRangeException(nameof(tradeId), "TradeId must be a positive integer.");
        if (valueDate == DateOnly.MinValue) throw new ArgumentOutOfRangeException(nameof(valueDate), "ValueDate must be a valid date.");
        if (valueDate == DateOnly.MaxValue) throw new ArgumentOutOfRangeException(nameof(valueDate), "ValueDate must be a valid date.");
        if (daysToExpiry < 0) throw new ArgumentOutOfRangeException(nameof(daysToExpiry), "DaysToExpiry cannot be negative.");

        TradeId = tradeId;
        TradeStatus = tradeStatus;
        ValueDate = valueDate;
        DaysToExpiry = daysToExpiry;
    }

    /// <summary>
    /// Formats the identifier as a dot-separated key.
    /// </summary>
    /// <returns>Formatted string: "TradeId.TradeStatus.ValueDate.DaysToExpiry".</returns>
    public string Format() => string.Create(null, stackalloc char[96], $"{TradeId}.{TradeStatus}.{ValueDate:yyyy-MM-dd}.{DaysToExpiry}");
}
