namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Projector;

/// <summary>Aggregate completed-write evidence, not an admission decision.</summary>
public sealed record TickStorageGenerationSnapshot(TickStorageGenerationTarget? Target,
    long CompletedWrites, DateTimeOffset? LastCompletedUtc);
