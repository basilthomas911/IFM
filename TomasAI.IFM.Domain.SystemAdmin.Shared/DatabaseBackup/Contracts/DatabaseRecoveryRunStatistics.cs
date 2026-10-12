using MessagePack;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[MessagePackObject]
public sealed record DatabaseRecoveryRunStatistics
{
    [Key(0)] public DatabaseEngine Engine { get; init; }
    [Key(1)] public DatabaseRecoveryPhase Phase { get; init; }
    [Key(2)] public DateTimeOffset? StartedUtc { get; init; }
    [Key(3)] public DateTimeOffset? CompletedUtc { get; init; }
    [Key(4)] public TimeSpan? Elapsed { get; init; }
    [Key(5)] public long? SourceBytes { get; init; }
    [Key(6)] public long? StoredBytes { get; init; }
    [Key(7)] public long? TransferredBytes { get; init; }
    [Key(8)] public long? RestoredBytes { get; init; }
    [Key(9)] public int? ArtifactCount { get; init; }
    [Key(10)] public double? AverageThroughputBytesPerSecond { get; init; }
    [Key(11)] public double? PeakThroughputBytesPerSecond { get; init; }
    [Key(12)] public int? RetryCount { get; init; }
    [Key(13)] public int? WarningCount { get; init; }
    [Key(14)] public TimeSpan? AchievedRpo { get; init; }
    [Key(15)] public TimeSpan? AchievedRto { get; init; }
}
