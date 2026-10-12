namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Projector;

/// <summary>Exact dataset generation observed at the tick storage boundary.</summary>
public sealed record TickStorageGenerationTarget(string Dataset, Guid GenerationId);
