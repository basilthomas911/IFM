using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command;

/// <summary>Handles <see cref="RecordDatabaseOperationStartedCommand"/> against the database backup aggregate.</summary>
public static class RecordDatabaseOperationStarted
{
    /// <summary>Applies the command's business guards and returns the resulting recovery operation identity.</summary>
    public static ServiceResult<GuidResult> Execute(this RecordDatabaseOperationStartedCommand command, DatabaseBackupCommandState state)
        => new ServiceOk<GuidResult>(new GuidResult(state.Execute(command).Value));
}
