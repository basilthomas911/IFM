namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;

/// <summary>Immutable audit projection row supplied by the Feed recovery projector.</summary>
public sealed record RecoveryLogRow(Guid RecordId, Guid OperationId, long Sequence, DateOnly ValueDate,
    string Role, string Dataset, string Kind, DateTimeOffset OccurredAtUtc, DateTime ReceivedAtUtc,
    string Digest, string Payload, long SourceEventId);
