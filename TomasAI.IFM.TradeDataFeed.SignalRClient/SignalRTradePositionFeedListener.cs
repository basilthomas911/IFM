using System;
using System.Collections.Generic;
using System.Linq;

using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.SignalRClient;
using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.TradePositionFeed.SignalRClient
{
    public class SignalRTradePositionFeedListener : SignalRServiceListener, ITradePositionFeedListener
    {
        private readonly string _baseUri;
        private Action<TradePositionUpdatedEvent> _eventAction;

        /// <summary>
        /// create trade position feed listener 
        /// </summary>
        /// <param name="options"></param>
        public SignalRTradePositionFeedListener(string baseUri)
        {
            _baseUri = baseUri;
        }

        public override string BaseUri => _baseUri;

        public override void ConnectEvents(HubConnection hubConnection)
        {
            hubConnection.On<TradePositionUpdatedEvent>("TradePositionUpdated", e => _eventAction(e));
        }

        /// <summary>
        /// listen for trade position data updates
        /// </summary>
        /// <param name="eventAction"></param>
        /// <returns></returns>
        public async Task StartAsync( Action<TradePositionUpdatedEvent> eventAction)
        {
            _eventAction = eventAction;
            await base.StartAsync();
        }

    }
}
