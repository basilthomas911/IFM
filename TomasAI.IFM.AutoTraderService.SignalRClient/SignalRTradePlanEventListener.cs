using System;
using System.Collections.Generic;
using System.Linq;

using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.SignalRClient;

namespace TomasAI.IFM.AlgoTrader.SignalRClient
{
    public class SignalRTradePlanEventListener : SignalRServiceListener, ITradePlanEventListener
    {
        private readonly string _baseUri;
        private Action<TradePlanUpdatedEvent> _eventAction;

        /// <summary>
        /// create trade plan event listener 
        /// </summary>
        /// <param name="baseUri"></param>
        public SignalRTradePlanEventListener(string baseUri)
        {
            _baseUri = baseUri;
        }

        public override string BaseUri => _baseUri;

        public override void ConnectEvents(HubConnection hubConnection)
        {
            hubConnection.On<TradePlanUpdatedEvent>("TradePlanUpdated", e => _eventAction(e));
        }

        /// <summary>
        /// listen for trade plan updates
        /// </summary>
        /// <typeparam name="TradePlanReadModel"></typeparam>
        /// <param name="eventAction"></param>
        /// <returns></returns>
        public async Task StartAsync(Action<TradePlanUpdatedEvent> eventAction)
        {
            _eventAction = eventAction;
            await base.StartAsync();
        }

    }
}
