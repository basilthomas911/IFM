using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Command.State;

public sealed record DatabaseBackupPolicyState
{
    public DatabaseBackupPolicyId? PolicyId { get; init; }
    public DatabaseBackupPolicyDefinition? Definition { get; init; }
    public long Revision { get; init; }
    public bool Enforced { get; init; }
    /// <summary>Applies this concern's accepted persisted facts without changing another concern.</summary>
    internal DatabaseBackupPolicyState Apply(DatabaseBackupEventContract e)
    {
        return e.Policy is null ? this : this with { Definition = e.Policy, Revision = e.Source.PolicyRevision,
            Enforced = e is DatabaseBackupPolicyEnforcedEvent };
    }
}
