using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
/// <summary>A bounded page of persisted phase observations and retained host output.</summary>
[MessagePackObject]
public sealed record DatabaseBackupLogReadModel
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(0)] public DatabaseBackupPhaseReadModel[] Phases { get; init; } = [];
    [Key(1)] public string Output { get; init; } = string.Empty;
    [Key(2)] public long NextOutputOffset { get; init; }
    [Key(3)] public bool EndOfOutput { get; init; } = true;
    [Key(4)] public bool OutputAvailable { get; init; }
}
