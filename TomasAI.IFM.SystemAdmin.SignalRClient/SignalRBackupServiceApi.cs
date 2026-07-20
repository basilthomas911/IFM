using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.SignalRClient;
using TomasAI.IFM.Shared.SystemAdmin.Events;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;

namespace TomasAI.IFM.SystemAdmin.SignalRClient
{
    public class SignalRBackupServiceApi : SignalRServiceApi, IDatabaseBackupServiceApi
    {
        private readonly string _baseUri;


        /// <summary>
        /// create market data feed service api
        /// </summary>
        /// <param name="options"></param>
        public SignalRBackupServiceApi(IBackupServiceApiOptions options)
        {
            _baseUri = options.BaseUri;
        }

        public override string BaseUri => _baseUri;
    
        public async Task ExecuteAsync(DatabaseBackupEvent backupEvent)
        {
            var systemAdminHub = await ConnectToHubAsync();
            await systemAdminHub?.SendAsync($"RunDatabaseBackup", backupEvent);
        }

        public Task ExecuteAsync(DatabaseBackupJobSubmittedEvent e)
        {
            throw new NotImplementedException();
        }

        public Task ExecuteAsync(DatabaseBackupJobCompletedEvent e)
        {
            throw new NotImplementedException();
        }

        public Task ExecuteAsync(DatabaseBackupCompletedEvent e)
        {
            throw new NotImplementedException();
        }
    }
}
