using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Event.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.EventHandling;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Event;

/// <summary>Handles <see cref="DatabaseRetentionExecutionCompletedEvent"/>.</summary>
public static class DatabaseRetentionExecutionCompleted
{

    extension(DatabaseRetentionExecutionCompletedEvent eventValue)
    {
        /// <summary>Translates and records this Database Backup service event through the command actor.</summary>
        public ValueTask ExecuteAsync(IEventActorContext<DatabaseBackupRetentionEventActor> context, ILogger<DatabaseBackupRetentionEventActor> logger)

        {
            ArgumentNullException.ThrowIfNull(eventValue);
            return DatabaseBackupServiceEventHandler.ExecuteAsync(eventValue, context, logger);
        }
    }
}
