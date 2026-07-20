using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.AspNetCore.SignalR;
using TomasAI.IFM.Shared.Log.ViewModels;
using TomasAI.IFM.Application.Core.Log;
using TomasAI.IFM.Application.Storage.LogDb;

namespace TomasAI.IFM.Application.Event.SignalR.Hubs 
{
    public class StatusConsoleHub : Hub
    {
        private ILogDbContext _logDb;

        public StatusConsoleHub(ILogDbContext logDb)
        {
            _logDb = logDb;
        }

        public async Task StatusConsoleLogUpdated(StatusConsoleLogReadModel e)
        {
            try
            {
                await Clients.AllExcept(Context.ConnectionId).SendAsync("StatusConsoleLogUpdated", e);
                await _logDb.InsertStatusConsoleLogAsync(new StatusConsoleLog(e));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public override async Task OnConnectedAsync() 
            => await base.OnConnectedAsync();

        public override async Task OnDisconnectedAsync(Exception ex) 
            => await base.OnDisconnectedAsync(ex);

    }
}
