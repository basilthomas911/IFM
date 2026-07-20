using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.AspNetCore.SignalR;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.Application.Event.SignalR.Hubs 
{
    public class OptionPricerHub : Hub
    {

        public OptionPricerHub()
        { }

        public async Task ExecuteAsync(SpreadDistributionJobReadModel spreadDistributionJob)
            => await Clients.AllExcept(Context.ConnectionId).SendAsync("ExecuteSpreadDistributionJobAsync", spreadDistributionJob);

        public async Task SpreadDistributionJobCompletedAsync(SpreadDistributionJobReadModel spreadDistributionJob)
            => await Clients.AllExcept(Context.ConnectionId).SendAsync("SpreadDistributionJobCompletedAsync", spreadDistributionJob);

        public override async Task OnConnectedAsync() 
            => await base.OnConnectedAsync();

        public override async Task OnDisconnectedAsync(Exception ex) 
            => await base.OnDisconnectedAsync(ex);

    }
}
