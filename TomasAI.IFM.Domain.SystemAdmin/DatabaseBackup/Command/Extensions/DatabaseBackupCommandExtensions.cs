using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.Extensions;

/// <summary>
/// Executes database recovery commands against their event-sourced aggregate and returns the resulting operation identity.
/// </summary>
public static class DatabaseBackupCommandExtensions
{
    /// <summary>Executes a public database recovery command.</summary>
    public static ServiceResult<GuidResult> Execute(
        this DatabaseBackupCommand command,
        DatabaseBackupCommandState state)
        => new ServiceOk<GuidResult>(new GuidResult(state.Execute(command).Value));

    /// <summary>Executes an internal database recovery service command.</summary>
    public static ServiceResult<GuidResult> Execute(
        this DatabaseBackupInternalCommand command,
        DatabaseBackupCommandState state)
        => new ServiceOk<GuidResult>(new GuidResult(state.Execute(command).Value));
}
