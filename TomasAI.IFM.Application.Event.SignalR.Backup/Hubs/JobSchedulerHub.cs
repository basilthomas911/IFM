using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.AspNetCore.SignalR;
using TomasAI.IFM.Shared.Log.ViewModels;
using TomasAI.IFM.Application.Core.Log;
using TomasAI.IFM.Application.Storage.LogDb;
using TomasAI.IFM.Shared.TaskScheduler;

namespace TomasAI.IFM.Application.Event.SignalR.Hubs 
{
    public class JobSchedulerHub : Hub
    {
        private IJobScheduler _jobScheduler;

        public JobSchedulerHub(IJobScheduler jobScheduler) => _jobScheduler = jobScheduler;

        public async Task StartAsync(DateTime valueDate)
        {
            await _jobScheduler.LoadAsync();
            await _jobScheduler.StartAsync(valueDate);
        }

        public async Task StopAsync() => await _jobScheduler.StopAsync();

        public async Task RefreshAsync(DateTime valueDate) => await _jobScheduler.RefreshAsync(valueDate);

        public override async Task OnConnectedAsync() => await base.OnConnectedAsync();

        public override async Task OnDisconnectedAsync(Exception ex) => await base.OnDisconnectedAsync(ex);

    }
}
