using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;

[MessagePackObject]
public sealed record DatabaseProtectionSetReadModel
{
    [Key(0)] public DatabaseProtectionSetId ProtectionSetId { get; init; }
    [Key(1)] public BackupSource Source { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(2)] public DatabaseEngine[] Engines { get; init; } = [];
    [Key(3)] public bool Enabled { get; init; }
    [Key(4)] public long PolicyRevision { get; init; }
}
