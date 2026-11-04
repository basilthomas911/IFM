using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.State;

[MessagePackObject]
public sealed record DatabaseRetentionState
{
    [Key(0)]
    public DatabaseRetentionPlanId? PlanId { get; init; }
    [Key(1)]
    public long PlanRevision { get; init; }
    [Key(2)]
    public DatabaseRecoveryOutcome Outcome { get; init; }
    [Key(3)]
    public long Revision { get; init; }
    /// <summary>Applies this concern's accepted persisted facts without changing another concern.</summary>
    internal DatabaseRetentionState Apply(DatabaseBackupEventContract e)
    {
        return e.RetentionPlanId is null ? this : this with { PlanId = e.RetentionPlanId, PlanRevision = e.RetentionPlanRevision,
            Outcome = e.Outcome, Revision = Revision + 1 };
    }
}
