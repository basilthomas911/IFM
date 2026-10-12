using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;

[MessagePackObject]
public sealed record DatabaseRecoveryRunStatsReadModel
{
    [Key(0)] public DatabaseRecoveryOperationId OperationId { get; init; }
    [Key(1)] public BackupSource Source { get; init; }
    [Key(2)] public long StatisticsRevision { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(3)] public DatabaseRecoveryRunStatistics[] Statistics { get; init; } = [];
}
