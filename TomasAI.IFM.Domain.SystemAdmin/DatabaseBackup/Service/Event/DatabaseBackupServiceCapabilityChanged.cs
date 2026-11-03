using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Event.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.EventHandling;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Event;

/// <summary>Handles <see cref="DatabaseBackupServiceCapabilityChangedEvent"/>.</summary>
public static class DatabaseBackupServiceCapabilityChanged
{
    /// <summary>Translates and records this Database Backup service event through the command actor.</summary>
    public static ValueTask ExecuteAsync(this DatabaseBackupServiceCapabilityChangedEvent eventValue, IEventActorContext<DatabaseBackupServiceEventActor> context, ILogger<DatabaseBackupServiceEventActor> logger)
        
    {
        ArgumentNullException.ThrowIfNull(eventValue);
        return DatabaseBackupServiceEventHandler.ExecuteAsync(eventValue, context, logger);
    }
}
