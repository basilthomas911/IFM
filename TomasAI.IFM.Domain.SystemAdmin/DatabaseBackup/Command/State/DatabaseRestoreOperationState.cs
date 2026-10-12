using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;

public sealed record DatabaseRestoreOperationState
{
    public DatabaseRestorePointId? RestorePointId { get; init; }
    public DatabaseRestoreClass RestoreClass { get; init; }
    public string FreshTargetProfile { get; init; } = string.Empty;
    public DatabaseFreshTargetDescriptor? FreshTarget { get; init; }
    public bool Approved { get; init; }
    public DatabaseCutoverState CutoverState { get; init; }
}
