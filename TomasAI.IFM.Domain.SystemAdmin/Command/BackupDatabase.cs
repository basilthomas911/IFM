using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.SystemAdmin.Commands;
using TomasAI.IFM.Shared.SystemAdmin.Events;
using TomasAI.IFM.Domain.SystemAdmin.Actor.Command.State;

namespace TomasAI.IFM.Domain.SystemAdmin.Actor.Command;

public static class BackupDatabase
{
    /// <summary>
    /// Executes a <see cref="BackupDatabaseCommand"/> against the provided <see cref="SystemAdminCommandState"/>.
    /// </summary>
    /// <param name="e">The backup database command to execute.</param>
    /// <param name="state">The current actor command state.</param>
    /// <returns><see langword="true"/> if the state was successfully updated; otherwise, <see langword="false"/>.</returns>
    public static bool Execute(this BackupDatabaseCommand e, SystemAdminCommandState state)
        => state.Update(e.CreateDatabaseBackupEvent(), e);

    /// <summary>
    /// Creates a <see cref="DatabaseBackupEvent"/> from a <see cref="BackupDatabaseCommand"/>.
    /// </summary>
    /// <param name="e">The source backup command containing entity identifiers, payload, and origin metadata.</param>
    /// <returns>A fully-populated backup event ready to be applied to actor state.</returns>
    internal static DatabaseBackupEvent CreateDatabaseBackupEvent(this BackupDatabaseCommand e)
    => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, DatabaseBackupEvent.Actor, DatabaseBackupEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            DatabaseName = e.DatabaseName,
            BackupType = e.BackupType,
            CommandTimeout = e.CommandTimeout,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        };
}
