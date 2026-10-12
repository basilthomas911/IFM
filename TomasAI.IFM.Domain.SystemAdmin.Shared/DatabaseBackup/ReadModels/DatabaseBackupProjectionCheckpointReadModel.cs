namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;

/// <summary>Describes the durable position of a database-backup projection.</summary>
public sealed record DatabaseBackupProjectionCheckpointReadModel(
    string ProjectorName,
    long LastEventId,
    long AppliedCount,
    DateTimeOffset UpdatedUtc);
