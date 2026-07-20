using System;
using System.Collections.Generic;
using System.Linq;

using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.AlgoTrader;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.SignalRClient;

namespace TomasAI.IFM.AlgoTrader.SignalRClient
{
    public class SignalRAlgoTraderEventListener : SignalRServiceListener,IAlgoTraderEventListener
    {
        private readonly string _baseUri;
        private readonly IAlgoTraderService _algoTraderService;

        /// <summary>
        /// create auto trader listener 
        /// </summary>
        /// <param name="listenerBaseUri"></param>
        public SignalRAlgoTraderEventListener(IAlgoTraderServiceApiOptions options, IAlgoTraderService algoTraderService)
        {
            _baseUri = options.BaseUri;
            _algoTraderService = algoTraderService;
        }

        public override string BaseUri => _baseUri;

        public override void ConnectEvents(HubConnection hubConnection)
        {
            hubConnection.On<TradeDistributionStatisticsUpdatedEvent>("UpdateTradePlan", 
                async e => await _algoTraderService.UpdateTradePlanAsync(e));
        }
       
    }
}
