namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;

/// <summary>Describes the durable position of a database-backup projection.</summary>
public sealed record DatabaseBackupProjectionCheckpointReadModel(
    string ProjectorName,
    long LastEventId,
    long AppliedCount,
    DateTimeOffset UpdatedUtc);

/// <summary>Describes the outcome of rebuilding database-backup projections.</summary>
public sealed record DatabaseBackupProjectionRebuildResult(
    int Applied,
    int AlreadyApplied,
    int Superseded,
    long LastEventId);
