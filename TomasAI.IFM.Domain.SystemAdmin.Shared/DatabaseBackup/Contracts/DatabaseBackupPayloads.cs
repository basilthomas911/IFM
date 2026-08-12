using MessagePack;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[MessagePackObject]
public sealed record DatabaseLogicalDestination([property: Key(0)] string Name, [property: Key(1)] bool Required);

[MessagePackObject]
public sealed record DatabaseFreshTargetDescriptor([property: Key(0)] string Profile, [property: Key(1)] string LogicalTarget);

[MessagePackObject]
public sealed record DatabaseRecoveryObjectives([property: Key(0)] TimeSpan RecoveryPointObjective, [property: Key(1)] TimeSpan RecoveryTimeObjective);

[MessagePackObject]
public sealed record DatabaseRetentionPolicy([property: Key(0)] int DailyCount, [property: Key(1)] int WeeklyCount, [property: Key(2)] int MonthlyCount);

[MessagePackObject]
public sealed record DatabaseVerificationPolicy([property: Key(0)] DatabaseVerificationLevel[] Levels, [property: Key(1)] TimeSpan MaximumVerificationAge);

[MessagePackObject]
public sealed record DatabaseBackupPolicyDefinition(
    [property: Key(0)] BackupSource[] EnabledSources,
    [property: Key(1)] DatabaseProtectionSetId[] ProtectedSets,
    [property: Key(2)] DatabaseRecoveryObjectives RecoveryObjectives,
    [property: Key(3)] DatabaseRetentionPolicy Retention,
    [property: Key(4)] DatabaseVerificationPolicy Verification);

[MessagePackObject]
public sealed record DatabaseOperationAcceptedResult
{
    [Key(0)] public DatabaseRecoveryOperationId OperationId { get; init; }
    [Key(1)] public DatabaseBackupSetId? BackupSetId { get; init; }
    [Key(2)] public BackupSource Source { get; init; }
    [Key(3)] public long PolicyRevision { get; init; }
    [Key(4)] public DatabaseRecoveryPhase InitialPhase { get; init; }
}

[MessagePackObject]
public sealed record DatabaseArtifactReplicaDescriptor
{
    [Key(0)] public DatabaseArtifactId ArtifactId { get; init; }
    [Key(1)] public DatabaseArtifactReplicaId ReplicaId { get; init; }
    [Key(2)] public DatabaseEngine Engine { get; init; }
    [Key(3)] public DatabaseArtifactReplicaState State { get; init; }
    [Key(4)] public string SafeDestinationReference { get; init; } = string.Empty;
    [Key(5)] public long? Bytes { get; init; }
}

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
