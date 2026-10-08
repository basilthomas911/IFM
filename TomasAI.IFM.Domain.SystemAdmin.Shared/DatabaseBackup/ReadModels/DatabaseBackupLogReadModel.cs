using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
/// <summary>A bounded page of persisted phase observations and retained host output.</summary>
[MessagePackObject]
public sealed record DatabaseBackupLogReadModel
{
    [Key(0)] public DatabaseBackupPhaseReadModel[] Phases { get; init; } = [];
    [Key(1)] public string Output { get; init; } = string.Empty;
    [Key(2)] public long NextOutputOffset { get; init; }
    [Key(3)] public bool EndOfOutput { get; init; } = true;
    [Key(4)] public bool OutputAvailable { get; init; }
}
/// <summary>A persisted point-in-time phase observation; percentage is a host milestone.</summary>
[MessagePackObject]
public sealed record DatabaseBackupPhaseReadModel
{
    [Key(0)] public long Revision { get; init; }
    [Key(1)] public DateTimeOffset ObservedUtc { get; init; }
    [Key(2)] public DatabaseRecoveryPhase Phase { get; init; }
    [Key(3)] public DatabaseRecoveryOutcome Outcome { get; init; }
    [Key(4)] public int ProgressPercent { get; init; }
}
/// <summary>Reads output only for a persisted, source-matched operation.</summary>
public interface IDatabaseBackupOutputReader
{
    /// <summary>Reads at most 64 KiB from the operation's output artifact.</summary>
    /// <summary>Reads the host-published allowlisted setup metadata.</summary>
    ValueTask<DatabaseBackupSetupReadModel> ReadSetupAsync(BackupSource source, CancellationToken cancellationToken) => ValueTask.FromResult(new DatabaseBackupSetupReadModel());
    ValueTask<DatabaseBackupLogReadModel> ReadAsync(BackupSource source, Guid operationId, long offset, CancellationToken cancellationToken);
}

/// <summary>Allowlisted host configuration references; contains no credentials or native connection strings.</summary>
[MessagePackObject]
public sealed record DatabaseBackupSetupReadModel
{
    [Key(0)] public Dictionary<string, string> BackupHostSettings { get; init; } = [];
    [Key(1)] public bool Available { get; init; }
}
