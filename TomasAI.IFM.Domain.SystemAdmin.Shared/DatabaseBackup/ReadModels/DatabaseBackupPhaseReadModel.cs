using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
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
