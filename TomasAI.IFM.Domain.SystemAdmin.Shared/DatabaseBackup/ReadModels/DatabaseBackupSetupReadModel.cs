using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;

/// <summary>Allowlisted host configuration references; contains no credentials or native connection strings.</summary>
[MessagePackObject]
public sealed record DatabaseBackupSetupReadModel
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Retains the existing public collection contract used by adapters and validation callers.")]
    [Key(0)] public Dictionary<string, string> BackupHostSettings { get; init; } = [];
    [Key(1)] public bool Available { get; init; }
}
