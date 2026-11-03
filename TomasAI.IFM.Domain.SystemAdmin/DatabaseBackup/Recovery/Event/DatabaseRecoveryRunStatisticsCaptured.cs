using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Event.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.EventHandling;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Event;

/// <summary>Handles <see cref="DatabaseRecoveryRunStatisticsCapturedEvent"/>.</summary>
public static class DatabaseRecoveryRunStatisticsCaptured
{
    /// <summary>Translates and records this Database Backup service event through the command actor.</summary>
    public static ValueTask ExecuteAsync(this DatabaseRecoveryRunStatisticsCapturedEvent eventValue, IEventActorContext<DatabaseRecoveryEventActor> context, ILogger<DatabaseRecoveryEventActor> logger)

    {
        ArgumentNullException.ThrowIfNull(eventValue);
        return DatabaseBackupServiceEventHandler.ExecuteAsync(eventValue, context, logger);
    }
}
