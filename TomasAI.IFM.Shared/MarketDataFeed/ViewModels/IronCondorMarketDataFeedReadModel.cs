using MessagePack;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing market data for an Iron Condor options strategy.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record IronCondorMarketDataFeedReadModel
{
    /// <summary>The underlying asset price.</summary>
    [Key(0)]
    public decimal AssetPrice { get; init; }

    /// <summary>Tick data for the short put option leg.</summary>
    [Key(1)]
    public FuturesOptionTickDataV2ReadModel ShortPutOptionData { get; init; }

    /// <summary>Tick data for the long put option leg.</summary>
    [Key(2)]
    public FuturesOptionTickDataV2ReadModel LongPutOptionData { get; init; }

    /// <summary>Tick data for the short call option leg.</summary>
    [Key(3)]
    public FuturesOptionTickDataV2ReadModel ShortCallOptionData { get; init; }

    /// <summary>Tick data for the long call option leg.</summary>
    [Key(4)]
    public FuturesOptionTickDataV2ReadModel LongCallOptionData { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public IronCondorMarketDataFeedReadModel() { }

    /// <summary>
    /// Full constructor to create an Iron Condor market data feed snapshot.
    /// </summary>
    /// <param name="assetPrice">Underlying asset price.</param>
    /// <param name="shortPutOptionData">Short put option tick data.</param>
    /// <param name="longPutOptionData">Long put option tick data.</param>
    /// <param name="shortCallOptionData">Short call option tick data.</param>
    /// <param name="longCallOptionData">Long call option tick data.</param>
    [SerializationConstructor]
    public IronCondorMarketDataFeedReadModel(
        decimal assetPrice,
        FuturesOptionTickDataV2ReadModel shortPutOptionData,
        FuturesOptionTickDataV2ReadModel longPutOptionData,
        FuturesOptionTickDataV2ReadModel shortCallOptionData,
        FuturesOptionTickDataV2ReadModel longCallOptionData)
    {
        AssetPrice = assetPrice;
        ShortPutOptionData = shortPutOptionData;
        LongPutOptionData = longPutOptionData;
        ShortCallOptionData = shortCallOptionData;
        LongCallOptionData = longCallOptionData;
    }
}
