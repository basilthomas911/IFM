using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Application.Event.SignalR.Hubs 
{
    public class TradeDataFeedHub : Hub
    {
        public TradeDataFeedHub()
        {
        }

        public async Task SpreadTradeDataUpdated(SpreadTradeDataViewModel spreadTrade)
            => await Clients.AllExcept(Context.ConnectionId).SendAsync("SpreadTradeDataUpdated", spreadTrade);

        public override async Task OnConnectedAsync() 
            => await base.OnConnectedAsync();

        public override async Task OnDisconnectedAsync(Exception ex) 
            => await base.OnDisconnectedAsync(ex);

    }
}
