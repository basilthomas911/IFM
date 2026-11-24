using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>Exact ordered source identity, independent of provider sequence/timestamp collisions.</summary>
[MessagePackObject]
public sealed record FuturesItiSourceCursor(
    [property: Key(0)] string Dataset,
    [property: Key(1)] int PublisherId,
    [property: Key(2)] long InstrumentId,
    [property: Key(3)] Guid Epoch,
    [property: Key(4)] long Ordinal,
    [property: Key(5)] long SourceSequence,
    [property: Key(6)] DateTime EventAtUtc,
    [property: Key(7)] DateTime ReceivedAtUtc);
