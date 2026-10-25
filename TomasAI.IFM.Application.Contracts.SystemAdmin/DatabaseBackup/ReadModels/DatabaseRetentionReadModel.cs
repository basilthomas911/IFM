using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;

[MessagePackObject]
public sealed record DatabaseRetentionReadModel
{
    [Key(0)] public DatabaseRetentionPlanId PlanId { get; init; }
    [Key(1)] public BackupSource Source { get; init; }
    [Key(2)] public long PlanRevision { get; init; }
    [Key(3)] public DateTimeOffset EvaluationBoundaryUtc { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(4)] public DatabaseRestorePointId[] Retain { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(5)] public DatabaseRestorePointId[] Delete { get; init; } = [];
    [Key(6)] public bool Approved { get; init; }
    [Key(7)] public DatabaseRecoveryOutcome Outcome { get; init; }
}
