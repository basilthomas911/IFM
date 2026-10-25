namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;

/// <summary>Describes the outcome of rebuilding database-backup projections.</summary>
public sealed record DatabaseBackupProjectionRebuildResult(
    int Applied,
    int AlreadyApplied,
    int Superseded,
    long LastEventId);
