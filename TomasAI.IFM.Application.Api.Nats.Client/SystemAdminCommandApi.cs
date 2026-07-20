using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.Commands;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>
/// Client implementation for system administration commands.
/// </summary>
/// <param name="commandSvc">Command service API client.</param>
public class SystemAdminCommandApi(IActorProducer actorProducer)
    : NatsCommandApi(actorProducer), ISystemAdminCommandApi
{
    /// <summary>
    /// Requests a backup of the specified database.
    /// </summary>
    /// <param name="databaseName">The database name to back up.</param>
    /// <param name="backupType">The type of backup (Full or Diff).</param>
    /// <param name="commandTimeout">Timeout for the backup command in seconds.</param>
    /// <returns>Command result containing the command id.</returns>
    public async Task<ServiceResult<Guid>> BackupDatabaseAsync(string databaseName, DatabaseBackupType backupType, int commandTimeout)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(databaseName);
            var entityId = new DatabaseBackupId(databaseName);
            var cmd = new BackupDatabaseCommand(databaseName, backupType, commandTimeout)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, BackupDatabaseCommand.Actor, BackupDatabaseCommand.Verb, entityId.Format()),
                ErrorCode = BackupDatabaseCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, BackupDatabaseCommand.ErrorId);
        }
        return serviceResult;
    }
}
