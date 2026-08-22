using MessagePack;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[MessagePackObject]
public sealed record DatabaseLogicalDestination([property: Key(0)] string Name, [property: Key(1)] bool Required);

[MessagePackObject]
public sealed record DatabaseFreshTargetDescriptor(
    [property: Key(0)] string Profile,
    [property: Key(1)] string LogicalTarget,
    [property: Key(2)] DateTimeOffset? RecoveryTargetUtc = null,
    [property: Key(3)] DatabaseArtifactReplicaId? PreferredReplicaId = null);

[MessagePackObject]
public sealed record DatabaseRecoveryObjectives([property: Key(0)] TimeSpan RecoveryPointObjective, [property: Key(1)] TimeSpan RecoveryTimeObjective);

[MessagePackObject]
public sealed record DatabaseRetentionPolicy([property: Key(0)] int DailyCount, [property: Key(1)] int WeeklyCount, [property: Key(2)] int MonthlyCount);

[MessagePackObject]
public sealed record DatabaseVerificationPolicy([property: Key(0)] DatabaseVerificationLevel[] Levels, [property: Key(1)] TimeSpan MaximumVerificationAge);

[MessagePackObject]
public sealed record DatabaseBackupLineage
{
    [Key(0)] public DatabaseBackupMode RequestedMode { get; init; }
    [Key(1)] public DatabaseBackupMode ResolvedMode { get; init; }
    [Key(2)] public DatabaseNativeBackupKind NativeKind { get; init; }
    [Key(3)] public DatabaseRestorePointId? BaseRestorePointId { get; init; }
    [Key(4)] public DatabaseRestorePointId? ParentRestorePointId { get; init; }
    [Key(5)] public int ChainDepth { get; init; }
    [Key(6)] public string NativeIdentity { get; init; } = string.Empty;

    public DatabaseBackupLineage NormalizeLegacyFull(DatabaseEngine engine = DatabaseEngine.None)
        => ResolvedMode != DatabaseBackupMode.None
            ? this
            : this with
            {
                RequestedMode = RequestedMode == DatabaseBackupMode.None ? DatabaseBackupMode.Full : RequestedMode,
                ResolvedMode = DatabaseBackupMode.Full,
                NativeKind = NativeKind != DatabaseNativeBackupKind.None
                    ? NativeKind
                    : engine switch
                    {
                        DatabaseEngine.PostgreSql => DatabaseNativeBackupKind.PostgreSqlBase,
                        DatabaseEngine.ScyllaDb => DatabaseNativeBackupKind.ScyllaManagerSnapshot,
                        _ => DatabaseNativeBackupKind.None
                    },
                ChainDepth = 0
            };

    public void Validate(bool resolvedRequired)
    {
        DatabaseBackupEnumValidation.RequireOptionalDefined(RequestedMode, nameof(RequestedMode));
        DatabaseBackupEnumValidation.RequireOptionalDefined(ResolvedMode, nameof(ResolvedMode));
        DatabaseBackupEnumValidation.RequireOptionalDefined(NativeKind, nameof(NativeKind));
        if (resolvedRequired && ResolvedMode == DatabaseBackupMode.None)
            throw new ArgumentException("A resolved backup mode is required.", nameof(ResolvedMode));
        if (resolvedRequired && NativeKind == DatabaseNativeBackupKind.None)
            throw new ArgumentException("A resolved native backup kind is required.", nameof(NativeKind));
        if (ChainDepth < 0) throw new ArgumentOutOfRangeException(nameof(ChainDepth));
        if (ResolvedMode == DatabaseBackupMode.Full
            && (ParentRestorePointId is not null || ChainDepth != 0))
            throw new ArgumentException("A full backup cannot have a parent or non-zero chain depth.");
        if (ResolvedMode == DatabaseBackupMode.Incremental
            && (BaseRestorePointId is null || ParentRestorePointId is null || ChainDepth <= 0))
            throw new ArgumentException("An incremental backup requires base, parent, and chain depth.");
        var incrementalNativeKind = NativeKind is DatabaseNativeBackupKind.PostgreSqlIncremental
            or DatabaseNativeBackupKind.ScyllaManagerDeduplicatedSnapshot;
        if (ResolvedMode == DatabaseBackupMode.Full && incrementalNativeKind)
            throw new ArgumentException("A full backup cannot use an incremental native kind.");
        if (ResolvedMode == DatabaseBackupMode.Incremental && !incrementalNativeKind)
            throw new ArgumentException("An incremental backup requires an incremental native kind.");
        if (NativeIdentity is null || NativeIdentity.Any(char.IsControl)
            || NativeIdentity.Length > DatabaseBackupContractLimits.SafeTextLength)
            throw new ArgumentOutOfRangeException(nameof(NativeIdentity));
    }
}

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
