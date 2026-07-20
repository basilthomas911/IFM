using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.AlgoTrader.ServiceApi;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.SignalRClient;

namespace TomasAI.IFM.AlgoTrader.SignalRClient
{
    public class SignalRAlgoTraderServiceApi :  SignalRServiceApi,IAlgoTraderServiceApi

    {
        private readonly string _baseUri;

        /// <summary>
        /// create auto trader service api
        /// </summary>
        /// <param name="options"></param>
        public SignalRAlgoTraderServiceApi(IAlgoTraderServiceApiOptions options)
        {
            _baseUri = options.BaseUri;
        }

        public override string BaseUri => _baseUri;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tradePlan"></param>
        /// <returns></returns>
        public async Task<ServiceResult> UpdateTradePlanAsync(TradeDistributionStatisticsUpdatedEvent tradePlanUpdating)
        {
            var serviceResult = default(ServiceResult);
            try
            {
                var eventHub = await ConnectToHubAsync();
                await eventHub?.SendAsync($"UpdateTradePlan", tradePlanUpdating);
                serviceResult = new ServiceOk();
            }
            catch(Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                serviceResult = new ServiceFailed(-40, ex.Message);
            }
            return serviceResult;
        }

        public async Task<ServiceResult> TradePlanUpdatedAsync(TradePlanUpdatedEvent tradePlanUpdated)
        {
            var serviceResult = default(ServiceResult);
            try
            {
                var eventHub = await ConnectToHubAsync();
                await eventHub?.SendAsync($"TradePlanUpdated", tradePlanUpdated);
                serviceResult = new ServiceOk();
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                serviceResult = new ServiceFailed(-40, ex.Message);
            }
            return serviceResult;

        }

    }
}
