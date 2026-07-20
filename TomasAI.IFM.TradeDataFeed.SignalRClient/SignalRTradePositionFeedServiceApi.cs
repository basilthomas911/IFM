using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.TradePositionFeed.SignalRClient
{
    public class SignalRTradePositionFeedServiceApi : ITradePositionFeedServiceApi
    {
        private HubConnection _eventHub;
        private readonly string _baseUri;

        /// <summary>
        /// create trade position feed service api
        /// </summary>
        /// <param name="options"></param>
        public SignalRTradePositionFeedServiceApi(ITradePositionFeedServiceApiOptions options)
        {
            _baseUri = options.BaseUri;
        }

        /// <summary>
        /// raise trade position event to all client listeners
        /// </summary>
        /// <param name="e">TradePositionUpdatedEvent</param>
        /// <returns></returns>
        public async Task<ServiceResult> TradePositionUpdatedAsync(TradePositionUpdatedEvent e)
        {
            var serviceResult = default(ServiceResult);
            try
            {
                _eventHub = _eventHub ?? await ConnectToHubAsync();
                await _eventHub?.InvokeAsync($"TradePositionUpdated", e);
                serviceResult = new ServiceOk();
            }
            catch(Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                serviceResult = new ServiceFailed(-30, ex.Message);
            }
            return serviceResult;
        }

        private async Task<HubConnection> ConnectToHubAsync()
        {
            var hubConnection = default(HubConnection);
            try
            {
                hubConnection = new HubConnectionBuilder()
                    .WithUrl(url: _baseUri,
                        configureHttpConnection: e => {
                            e.UseDefaultCredentials = true;
                            e.Transports = HttpTransportType.WebSockets;
                            e.CloseTimeout = TimeSpan.FromMinutes(10);
                    })
                    //.ConfigureLogging(logging => {
                    //      logging.AddDebug();
                    //      logging.SetMinimumLevel(LogLevel.Trace);
                    //})
                    .Build();
                await hubConnection.StartAsync();
                hubConnection.Closed += async (error) => {
                    await Task.Delay(new Random().Next(0, 5) * 1000);
                    _eventHub = await ConnectToHubAsync();
                };
            }
            catch
            {
                if (hubConnection != null)
                {
                    await hubConnection.DisposeAsync();
                    hubConnection = null;
                }
            }
            return hubConnection;
        }
       
    }
}
