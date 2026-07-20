using System;
using TomasAI.IFM.SignalRClient;
using Microsoft.AspNetCore.SignalR.Client;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.SystemAdmin.Events;

namespace TomasAI.IFM.SystemAdmin.SignalRClient
{
    public class SignalRBackupServiceListener : SignalRServiceListener, IBackupServiceListener
    {
        private readonly string _baseUri;
        private readonly IDatabaseBackupServiceApi _backupServiceApi;

        public SignalRBackupServiceListener(IDatabaseBackupServiceApi backupServiceApi,  IBackupServiceListenerOptions options)
        {
            _backupServiceApi = backupServiceApi;
            _baseUri = options.BaseUri;
        }

        public override string BaseUri => _baseUri;

        public override void ConnectEvents(HubConnection hubConnection)
        {
            hubConnection.On<DatabaseBackupEvent>("DatabaseBackup", async e => await _backupServiceApi.ExecuteAsync(e));
        }
    }
}
