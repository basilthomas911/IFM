using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

/// <summary>Immutable source-journal records; source timestamps stay inside their original event payloads.</summary>
[MessagePackObject]
public sealed record RawFuturesStorageRecord(
    [property: Key(0)] long Ordinal,
    [property: Key(1)] Guid PhysicalGeneration,
    [property: Key(2)] byte Kind,
    [property: Key(3)] byte[] Payload);

/// <summary>Bounded delivery identity, independent of physical source generations and actor mailboxes.</summary>
[MessagePackObject]
public sealed record RawFuturesStorageBatch(
    [property: Key(0)] Guid SourceEpoch,
    [property: Key(1)] long EndOffset,
    [property: Key(2)] RawFuturesStorageRecord[] Records)
{
    public const int MaximumCount = 64;
    public const int MaximumBytes = 64_000;
}
