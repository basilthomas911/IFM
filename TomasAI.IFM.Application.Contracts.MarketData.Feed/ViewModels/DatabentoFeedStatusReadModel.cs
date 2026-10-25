using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

[MessagePackObject(false)]
public sealed record DatabentoFeedStatusReadModel
{
    [Key(0)] public ulong FeedInstanceId { get; init; }
    [Key(1)] public string Dataset { get; init; } = string.Empty;
    [Key(2)] public string FeedKind { get; init; } = string.Empty;
    [Key(3)] public string Criticality { get; init; } = string.Empty;
    [Key(4)] public string MajorStatus { get; init; } = string.Empty;
    [Key(5)] public string NativeState { get; init; } = string.Empty;
    [Key(6)] public bool ProducerAlive { get; init; }
    [Key(7)] public bool AggregationWorkerRunning { get; init; }
    [Key(8)] public int ExpectedSubscriptions { get; init; }
    [Key(9)] public int ReceivedSubscriptions { get; init; }
    [Key(10)] public ulong ProviderMessageCount { get; init; }
    [Key(11)] public long LastProviderMessageAgeTicks { get; init; }
    [Key(12)] public ulong RingUsed { get; init; }
    [Key(13)] public ulong RingCapacity { get; init; }
    [Key(14)] public string FailureDetail { get; init; } = string.Empty;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(15)] public string[] ContractIds { get; init; } = [];
}
