using MessagePack;

namespace TomasAI.IFM.Framework.MarketData.Contracts;

/// <summary>Observed option statistics with independent exchange reference dates; null is unavailable, not zero.</summary>
[MessagePackObject]
public sealed record OptionChainStatisticsSnapshot(
    [property: Key(0)] string ContractId,
    [property: Key(1)] long? Volume,
    [property: Key(2)] DateOnly? VolumeValueDate,
    [property: Key(3)] long? OpenInterest,
    [property: Key(4)] DateOnly? OpenInterestValueDate);
