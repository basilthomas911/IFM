using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.AspNetCore.SignalR;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Application.Event.SignalR.Hubs 
{
    public class MarketDataFeedHub : Hub
    {
        public MarketDataFeedHub()
        {
        }

        public async Task FuturesEodDataUpdated(FuturesEodDataViewModel futuresEodData)
            => await Clients.AllExcept(Context.ConnectionId).SendAsync("FuturesEodDataUpdated", futuresEodData);

        public override async Task OnConnectedAsync() 
            => await base.OnConnectedAsync();

        public override async Task OnDisconnectedAsync(Exception ex) 
            => await base.OnDisconnectedAsync(ex);

    }
}
