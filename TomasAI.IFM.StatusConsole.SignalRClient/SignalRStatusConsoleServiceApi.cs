using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log.ViewModels;
using TomasAI.IFM.Shared.Log.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.SignalRClient;

namespace TomasAI.IFM.StatusConsole.SignalRClient
{
    public class SignalRStatusConsoleServiceApi : SignalRServiceApi, IStatusConsoleServiceApi
    {
        private readonly string _baseUri;

        /// <summary>
        /// create market data feed service api
        /// </summary>
        /// <param name="options"></param>
        public SignalRStatusConsoleServiceApi(IStatusConsoleServiceApiOptions options)
        {
            _baseUri = options.BaseUri;
        }

        public override string BaseUri => _baseUri;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="statusConsoleLog"></param>
        /// <returns></returns>
        public async Task<ServiceResult> StatusConsoleLogUpdatedAsync(StatusConsoleLoggedEvent e)
        {
            var serviceResult = default(ServiceResult);
            try
            {
                e.StatusConsoleLog.StatusCodeType = e.StatusConsoleLog.StatusCode == 0 ? StatusCodeType.Ok: StatusCodeType.Error;
                var statusConsoleHub = await ConnectToHubAsync();
                await statusConsoleHub?.SendAsync($"StatusConsoleLogUpdated", e);
                serviceResult = new ServiceOk();
            }
            catch(Exception ex)
            {
                serviceResult = new ServiceFailed(-35, ex.GetErrorMessage());
            }
            return serviceResult;
        }

        
    }
}
