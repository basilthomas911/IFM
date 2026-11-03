using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Event.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.EventHandling;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Event;

/// <summary>Handles <see cref="DatabaseBackupServiceReconciliationEvent"/>.</summary>
public static class DatabaseBackupServiceReconciliation
{

    extension(DatabaseBackupServiceReconciliationEvent eventValue)
    {
        /// <summary>Translates and records this Database Backup service event through the command actor.</summary>
        public ValueTask ExecuteAsync(IEventActorContext<DatabaseBackupEventActor> context, ILogger<DatabaseBackupEventActor> logger)

        {
            ArgumentNullException.ThrowIfNull(eventValue);
            return DatabaseBackupServiceEventHandler.ExecuteAsync(eventValue, context, logger);
        }
    }
}
