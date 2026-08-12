namespace TomasAI.IFM.Application.Storage;

public sealed record DatabaseBackupProjectionCheckpoint(
    string ProjectorName,
    long LastEventId,
    long AppliedCount,
    DateTimeOffset UpdatedUtc);

public sealed record DatabaseBackupProjectionRebuildResult(
    int Applied,
    int AlreadyApplied,
    int Superseded,
    long LastEventId);
