using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

/// <summary>
/// Provider-neutral, complete open/high/low snapshot for one futures trading session.
/// </summary>
[MessagePackObject]
public readonly record struct FuturesSessionStatisticsSnapshot(
    [property: Key(0)] string ContractId,
    [property: Key(1)] DateOnly ValueDate,
    [property: Key(2)] decimal OpenPrice,
    [property: Key(3)] decimal HighPrice,
    [property: Key(4)] decimal LowPrice,
    [property: Key(5)] uint SourceSequence,
    [property: Key(6)] long EventTimestampNanoseconds)
{
    [IgnoreMember]
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(ContractId)
        && ValueDate != default
        && OpenPrice > 0m
        && HighPrice > 0m
        && LowPrice > 0m
        && HighPrice >= LowPrice
        && OpenPrice >= LowPrice
        && OpenPrice <= HighPrice;
}
