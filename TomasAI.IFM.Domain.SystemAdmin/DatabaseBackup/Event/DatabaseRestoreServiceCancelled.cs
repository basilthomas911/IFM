using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Model;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event;

/// <summary>Handles <see cref="DatabaseRestoreServiceCancelledEvent"/>.</summary>
public static class DatabaseRestoreServiceCancelled
{
    /// <summary>Translates and records this Database Backup service event through the command actor.</summary>
    public static ValueTask ExecuteAsync(this DatabaseRestoreServiceCancelledEvent eventValue, IEventActorContext<DatabaseBackupEventActor> context, ILogger<DatabaseBackupEventActor> logger)
        => DatabaseBackupServiceEventHandler.ExecuteAsync(eventValue, context, logger);
}
