using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Queries;
using TomasAI.IFM.Shared.SignalR;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.OptionPricer;

namespace TomasAI.IFM.Application.EventBus.SignalRClient
{
    public class SignalRSpreadDistributionService : ISpreadDistributionService
    {
        private const int _errorCode = 1234;
        private HubConnection _optionPricerHub;
        private readonly string _optionPricerHubUri;

        public SignalRSpreadDistributionService(string baseUri)
        {
            _optionPricerHubUri = baseUri;
            _optionPricerHub = _optionPricerHub ?? ConnectToOptionPricerHub();
        }
        

        public async Task<ServiceResult> SubmitAsync(int orderId,
            int tradeId,
            TradeType tradeType,
            TradeStatus tradeStatus,
            DateTime valueDate,
            OptionStyle optionStyle,
            OptionType optionType) 
        {
            var serviceResult = default(ServiceResult);
            try
            {
                _optionPricerHub = _optionPricerHub ?? ConnectToOptionPricerHub();
                var serializedServiceResult = await _optionPricerHub?.InvokeAsync<string>($"SubmitAsync", tradeId, tradeType, tradeStatus, valueDate, optionStyle, optionType);
                serviceResult = JsonConvert.DeserializeObject<ServiceResult>(serializedServiceResult);
            }
            catch(Exception ex)
            {
                while (ex.InnerException != null)
                    ex = ex.InnerException;
                serviceResult = new ServiceResult(false, _errorCode, ex.Message);
            }
            return serviceResult;
        }

        public void Start()
            => _optionPricerHub.SendAsync("Start");

        public void Stop()
            => _optionPricerHub.SendAsync("Stop");


        public async Task<OptionPricerDeviceCollection> GetOptionPricerDevicesAsync()
        {
            var serializedOptionPricerDevices = await _optionPricerHub?.InvokeAsync<string>("GetOptionPricerDevices");
            var optionPricerDevices = JsonConvert.DeserializeObject<OptionPricerDeviceCollection>(serializedOptionPricerDevices);
            return optionPricerDevices;
        }

        private HubConnection ConnectToOptionPricerHub()
        {
            var hubConnection = default(HubConnection);
            try
            {
                hubConnection = new HubConnectionBuilder()
                    .WithUrl(url: _optionPricerHubUri,
                        configureHttpConnection: e => {
                            e.UseDefaultCredentials = true;
                            e.CloseTimeout = TimeSpan.FromHours(1);
                        })
                    .Build();
                hubConnection.StartAsync();
                hubConnection.ServerTimeout = TimeSpan.FromHours(1);
                hubConnection.HandshakeTimeout = TimeSpan.FromHours(1);
                hubConnection.Closed += HubConnection_Closed;
            }
            catch
            {
                if (hubConnection != null)
                {
                    hubConnection.DisposeAsync();
                    hubConnection = null;
                }
            }
            return hubConnection;
        }

        private async Task HubConnection_Closed(Exception arg)
        {
            _optionPricerHub = null;
            if (arg != null)
                throw new ServiceException(-1, arg.Message);
        }

   
    }
}
