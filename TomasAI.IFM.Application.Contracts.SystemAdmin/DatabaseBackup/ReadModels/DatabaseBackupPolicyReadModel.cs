using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;

[MessagePackObject]
public sealed record DatabaseBackupPolicyReadModel
{
    [Key(0)] public DatabaseBackupPolicyId PolicyId { get; init; }
    [Key(1)] public string EnvironmentIdentity { get; init; } = string.Empty;
    [Key(2)] public long Revision { get; init; }
    [Key(3)] public DatabaseBackupPolicyDefinition Definition { get; init; } = new([], [], new(TimeSpan.Zero, TimeSpan.Zero), new(0, 0, 0), new([], TimeSpan.Zero));
    [Key(4)] public bool Enforced { get; init; }
}
