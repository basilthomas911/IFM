using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.Model;
/// <summary>Ordered immutable lifecycle changes or a rejected business decision.</summary>
internal sealed record DatabaseBackupTransition(IReadOnlyList<DatabaseBackupChange> LifecycleChanges, string? RejectionReason = null);
