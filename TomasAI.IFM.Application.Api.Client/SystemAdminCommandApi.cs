using System;
using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.Commands;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Api.Client;

/// <summary>
/// Client implementation for system administration commands.
/// </summary>
/// <param name="commandSvc">Command service API client.</param>
public class SystemAdminCommandApi(ICommandServiceApi commandSvc) : ISystemAdminCommandApi
{
    readonly ICommandServiceApi _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// Requests a backup of the specified database.
    /// </summary>
    /// <param name="databaseName">The database name to back up.</param>
    /// <param name="backupType">The type of backup (Full or Diff).</param>
    /// <param name="commandTimeout">Timeout for the backup command in seconds.</param>
    /// <returns>Command result containing the command id.</returns>
    public async Task<ServiceResult<Guid>> BackupDatabaseAsync(string databaseName, DatabaseBackupType backupType, int commandTimeout)
        => await new BackupDatabaseCommand(databaseName, backupType, commandTimeout)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(SystemAdminUriPath.BackupDatabase, e));
}
