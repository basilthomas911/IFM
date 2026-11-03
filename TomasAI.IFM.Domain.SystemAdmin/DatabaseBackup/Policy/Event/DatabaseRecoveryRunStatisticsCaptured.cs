using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Event.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.EventHandling;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Event;

/// <summary>Handles <see cref="DatabaseRecoveryRunStatisticsCapturedEvent"/>.</summary>
public static class DatabaseRecoveryRunStatisticsCaptured
{

    extension(DatabaseRecoveryRunStatisticsCapturedEvent eventValue)
    {
        /// <summary>Translates and records this Database Backup service event through the command actor.</summary>
        public ValueTask ExecuteAsync(IEventActorContext<DatabaseBackupPolicyEventActor> context, ILogger<DatabaseBackupPolicyEventActor> logger)

        {
            ArgumentNullException.ThrowIfNull(eventValue);
            return DatabaseBackupServiceEventHandler.ExecuteAsync(eventValue, context, logger);
        }
    }
}
