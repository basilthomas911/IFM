using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Command.State;

[MessagePackObject]
public sealed record DatabaseBackupPolicyState
{
    [Key(0)]
    public DatabaseBackupPolicyId? PolicyId { get; init; }
    [Key(1)]
    public DatabaseBackupPolicyDefinition? Definition { get; init; }
    [Key(2)]
    public long Revision { get; init; }
    [Key(3)]
    public bool Enforced { get; init; }
    /// <summary>Applies this concern's accepted persisted facts without changing another concern.</summary>
    internal DatabaseBackupPolicyState Apply(DatabaseBackupEventContract e)
    {
        return e.Policy is null ? this : this with { Definition = e.Policy, Revision = e.Source.PolicyRevision,
            Enforced = e is DatabaseBackupPolicyEnforcedEvent };
    }
}
