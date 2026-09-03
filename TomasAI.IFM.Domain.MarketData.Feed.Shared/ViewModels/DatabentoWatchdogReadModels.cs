using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

[MessagePackObject(false)]
public sealed record DatabentoContractAssignmentReadModel
{
    [Key(0)] public string Role { get; init; } = string.Empty;
    [Key(1)] public string RootSymbol { get; init; } = string.Empty;
    [Key(2)] public string ContractId { get; init; } = string.Empty;
    [Key(3)] public DateOnly LastTradeDate { get; init; }
    [Key(4)] public DateOnly NextRolloverDate { get; init; }
    [Key(5)] public long RowVersion { get; init; }
    [Key(6)] public DateTime UpdatedOnUtc { get; init; }
}

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
    [Key(15)] public string[] ContractIds { get; init; } = [];
}

[MessagePackObject(false)]
public sealed record DatabentoReadinessReadModel
{
    [Key(0)] public string State { get; init; } = string.Empty;
    [Key(1)] public string DisplayHealth { get; init; } = string.Empty;
    [Key(2)] public bool CoreReady { get; init; }
    [Key(3)] public DateOnly? ValueDate { get; init; }
    [Key(4)] public Guid CorrelationId { get; init; }
    [Key(5)] public Guid NativeGeneration { get; init; }
    [Key(6)] public int RecoveryAttempt { get; init; }
    [Key(7)] public string Reason { get; init; } = string.Empty;
    [Key(8)] public DateTime ChangedOnUtc { get; init; }
    [Key(9)] public DateTime? NextRetryOnUtc { get; init; }
    [Key(10)] public DatabentoFeedStatusReadModel[] Feeds { get; init; } = [];
}

[MessagePackObject(false)]
public sealed record DatabentoWatchdogObservationReadModel
{
    [Key(0)] public long Id { get; init; }
    [Key(1)] public Guid ObservationId { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
    [Key(3)] public DateOnly ValueDate { get; init; }
    [Key(4)] public DateTime ObservedOnUtc { get; init; }
    [Key(5)] public string OperationReason { get; init; } = string.Empty;
    [Key(6)] public string MajorStatus { get; init; } = string.Empty;
    [Key(7)] public string DisplayHealth { get; init; } = string.Empty;
    [Key(8)] public bool CoreReady { get; init; }
    [Key(9)] public int RecoveryAttempt { get; init; }
    [Key(10)] public string FailureStage { get; init; } = string.Empty;
    [Key(11)] public string FailureDetail { get; init; } = string.Empty;
    [Key(12)] public DatabentoFeedStatusReadModel[] Feeds { get; init; } = [];
    [Key(13)] public long RowVersion { get; init; }
}
