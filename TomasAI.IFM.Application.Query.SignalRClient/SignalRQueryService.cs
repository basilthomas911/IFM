using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Queries;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.SignalRClient;

namespace TomasAI.IFM.Application.Query.SignalRClient
{
    public class SignalRQueryService : SignalRServiceApi, IQueryService
    {
        private readonly static SemaphoreSlim _queryServiceSlim = new SemaphoreSlim(1, 1);
        private readonly string _baseUri;

        public SignalRQueryService(string baseUri)
        {
            _baseUri = baseUri;
        }

        public override string BaseUri => _baseUri;

        public async Task<ServiceResult<TResult>> ExecuteQueryAsync<TResult>(IQuery<TResult> query)
        {

            var serviceResult = default(ServiceResult<TResult>);
            await _queryServiceSlim.WaitAsync();
            try
            {
                var serializedQuery = JsonConvert.SerializeObject(query, Formatting.None);
                var queryHub = await ConnectToHubAsync();
                var serializedServiceResult = await queryHub?.InvokeAsync<string>($"ExecuteQueryAsync", query.GetType().AssemblyQualifiedName, serializedQuery);
                serviceResult = JsonConvert.DeserializeObject<ServiceResult<TResult>>(serializedServiceResult);
                Debug.WriteLine($"ExecuteQueryAsync: {query.GetType().Name} ResultType: {typeof(TResult).Name}");
            }
            catch(Exception ex)
            {
                serviceResult = new ServiceResult<TResult>(default(TResult), false, -20, ex.Message);
            }
            finally
            {
                _queryServiceSlim.Release();
            }
            return serviceResult;
        }
       
    }
}
