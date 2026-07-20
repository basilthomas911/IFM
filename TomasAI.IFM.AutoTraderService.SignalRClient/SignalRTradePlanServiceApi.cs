using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.SignalRClient;

namespace TomasAI.IFM.AlgoTrader.SignalRClient
{
    public class SignalRTradePlanServiceApi : SignalRServiceApi, ITradePlanServiceApi
    {
        private readonly string _baseUri;

        /// <summary>
        /// create auto trader service api
        /// </summary>
        /// <param name="options"></param>
        public SignalRTradePlanServiceApi(ITradePlanServiceApiOptions options)
        {
            _baseUri = options.BaseUri;
        }

        public override string BaseUri => _baseUri;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tradePlan"></param>
        /// <returns></returns>
        public async Task<ServiceResult> TradePlanUpdatedAsync(TradePlanReadModel tradePlan)
        {
            var serviceResult = default(ServiceResult);
            try
            {
                var autoTraderHub = await ConnectToHubAsync();
                await autoTraderHub?.SendAsync($"TradePlanUpdated", tradePlan);
                serviceResult = new ServiceOk();
            }
            catch(Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                serviceResult = new ServiceFailed(-40, ex.Message);
            }
            return serviceResult;

        }

    }
}
