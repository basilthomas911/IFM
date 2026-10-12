using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(12)] public DatabentoFeedStatusReadModel[] Feeds { get; init; } = [];
    [Key(13)] public long RowVersion { get; init; }
}
