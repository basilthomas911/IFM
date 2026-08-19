using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

/// <summary>
/// Provider-neutral price and volume snapshot for one futures trading session.
/// </summary>
public enum FuturesSessionVolumeQuality : byte
{
    Unknown = 0,
    Bootstrapping = 1,
    ObservedComplete = 2,
    OfficialFinal = 3
}

[MessagePackObject]
public readonly record struct FuturesSessionStatisticsSnapshot(
    [property: Key(0)] string ContractId,
    [property: Key(1)] DateOnly ValueDate,
    [property: Key(2)] decimal OpenPrice,
    [property: Key(3)] decimal HighPrice,
    [property: Key(4)] decimal LowPrice,
    [property: Key(5)] uint SourceSequence,
    [property: Key(6)] long EventTimestampNanoseconds,
    [property: Key(7)] long Volume = 0,
    [property: Key(8)] FuturesSessionVolumeQuality VolumeQuality =
        FuturesSessionVolumeQuality.Unknown)
{
    [IgnoreMember]
    public bool HasPriceStatistics =>
        !string.IsNullOrWhiteSpace(ContractId)
        && ValueDate != default
        && OpenPrice > 0m
        && HighPrice > 0m
        && LowPrice > 0m
        && HighPrice >= LowPrice
        && OpenPrice >= LowPrice
        && OpenPrice <= HighPrice;

    [IgnoreMember]
    public bool HasVolume =>
        !string.IsNullOrWhiteSpace(ContractId)
        && ValueDate != default
        && Volume >= 0
        && VolumeQuality is FuturesSessionVolumeQuality.ObservedComplete
            or FuturesSessionVolumeQuality.OfficialFinal;

    [IgnoreMember]
    public bool IsComplete => HasPriceStatistics;

    [IgnoreMember]
    public bool HasAnyData => HasPriceStatistics || HasVolume;
}
