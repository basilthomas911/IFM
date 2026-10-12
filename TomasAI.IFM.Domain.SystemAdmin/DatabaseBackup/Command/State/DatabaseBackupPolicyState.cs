using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;

public sealed record DatabaseBackupPolicyState
{
    public DatabaseBackupPolicyId? PolicyId { get; init; }
    public DatabaseBackupPolicyDefinition? Definition { get; init; }
    public long Revision { get; init; }
    public bool Enforced { get; init; }
}
