using MessagePack;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a futures option spread with short and long legs.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionSpreadDataReadModel
{
    /// <summary>The short leg of the spread.</summary>
    [Key(0)]
    public FuturesOptionDataReadModel ShortLeg { get; init; }

    /// <summary>The long leg of the spread.</summary>
    [Key(1)]
    public FuturesOptionDataReadModel LongLeg { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public FuturesOptionSpreadDataReadModel() { }

    /// <summary>
    /// MessagePack serialization constructor (Key indices must match property Key attributes).
    /// </summary>
    /// <param name="shortLeg">Short leg of the spread (Key 0).</param>
    /// <param name="longLeg">Long leg of the spread (Key 1).</param>
    [SerializationConstructor]
    public FuturesOptionSpreadDataReadModel(
        FuturesOptionDataReadModel shortLeg,
        FuturesOptionDataReadModel longLeg)
    {
        ShortLeg = shortLeg;
        LongLeg = longLeg;
    }
}
