using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using TomasAI.IFM.Shared.Trade.ViewModels;


namespace TomasAI.IFM.Application.Event.SignalR.Hubs
{
    public class AutoTraderHub : Hub
    {
        public AutoTraderHub()
        {
        }

        public async Task TradePlanUpdated(TradePlanReadModel tradePlan)
            => await Clients.AllExcept(Context.ConnectionId).SendAsync("TradePlanUpdated", tradePlan);

        public override async Task OnConnectedAsync()
            => await base.OnConnectedAsync();

        public override async Task OnDisconnectedAsync(Exception ex)
            => await base.OnDisconnectedAsync(ex);
    }
}
