using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.Commands;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Command.Client;

public class SystemAdminCommandApi(ICommandService commandSvc) : ISystemAdminCommandApi
{
    const string SystemAdminController = "SystemAdmin";
    readonly ICommandService _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// backup selected database
    /// </summary>
    /// <param name="databaseName"></param>
    /// <param name="backupType"></param>
    /// <param name="commandTimeout"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> BackupDatabaseAsync(string databaseName, DatabaseBackupType backupType, int commandTimeout)
         => await new BackupDatabaseCommand (databaseName, backupType, commandTimeout)
            .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, SystemAdminController));

}
