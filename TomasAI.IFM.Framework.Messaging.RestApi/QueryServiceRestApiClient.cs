using System;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Framework.Messaging.RestApi
{
    public class QueryServiceRestApiClient : IQueryService
    {
        readonly Uri _baseUri;
        readonly HttpClient _httpClient;
        // readonly IHttpClientFactory _httpClientFactory;
        readonly IJsonSerializer _serializer;

        public QueryServiceRestApiClient(IQueryServiceRestApiOptions options, IJsonSerializer serializer)
        {
            IsArgumentNull.Check(options.BaseUri);
            _serializer = IsArgumentNull.Set(serializer);
            //_httpClientFactory = IsArgumentNull.Set(httpClientFactory);
            _baseUri = new Uri(options.BaseUri);
            _httpClient = new HttpClient(new HttpClientHandler { UseDefaultCredentials = true });
            _httpClient.Timeout = TimeSpan.FromMinutes(10);
        }

        public async Task<ServiceResult<TResult>> ExecuteApiQueryAsync<TResult>(IQuery<TResult> query, string controllerName)
        {
            try
            {
                if (query is null) throw new ArgumentNullException(nameof(query));
                if (string.IsNullOrWhiteSpace(controllerName)) throw new ArgumentNullException(nameof(controllerName));

                //using HttpClient httpClient = _httpClientFactory.CreateClient("HttpRestApi");
                var queryName = query.GetType().Name.ToLower().Replace("get", "").Replace("query", "");
                var queryUri = string.IsNullOrWhiteSpace(query.QueryParams)
                    ? $"api/{controllerName}/{queryName}"
                    : $"api/{controllerName}/{queryName}?{query.QueryParams}";
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(_baseUri, queryUri),
                 };
                request.Headers.Add("X-QueryTypeName", query.GetType().AssemblyQualifiedName);
                request.Headers.Add("Cache-Control", "max-age=0, no-cache, must-revalidate, proxy-revalidate");
                var response = await _httpClient.SendAsync(request);
                var serviceResult = response.StatusCode switch
                {
                    HttpStatusCode.OK => await GetServiceResult(response),
                    HttpStatusCode.InternalServerError => await GetServiceFailed(response),
                    _ => new ServiceFailed<TResult>(query.ErrorCode, $"{response.StatusCode} - {response!.RequestMessage!.RequestUri}")
                };
                return serviceResult;
            }
            catch (Exception ex)
            {
                return new ServiceFailed<TResult>(query.ErrorCode, ex.Message);
            }

            async Task<ServiceResult<TResult>> GetServiceResult(HttpResponseMessage response)
            {
                var content = await response.Content.ReadAsStringAsync();
                return (_serializer.Deserialize(content, typeof(ServiceResult<TResult>)) as ServiceResult<TResult>)!;
            }

            async Task<ServiceResult<TResult>> GetServiceFailed(HttpResponseMessage response)
            {
                var content = await response.Content.ReadAsStringAsync();
                return (_serializer.Deserialize(content, typeof(ServiceFailed<TResult>)) as ServiceResult<TResult>)!;
            }

        }

        public async Task<ServiceResult<TResult>> PostApiQueryAsync<TResult>(IQuery<TResult> query, string controllerName)
        {
            try
            {
                if (query is null) throw new ArgumentNullException(nameof(query));
                if (string.IsNullOrWhiteSpace(controllerName)) throw new ArgumentNullException(nameof(controllerName));

                var queryName = query.GetType().Name.ToLower().Replace("get", "").Replace("query", "");
                var queryUri = $"api/{controllerName}/{queryName}";
                var serializedContent = _serializer.Serialize(query);
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(_baseUri, queryUri),
                    Content = new StringContent(serializedContent, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("X-QueryTypeName", query.GetType().AssemblyQualifiedName);
                var response = await _httpClient.SendAsync(request);
                var serviceResult = response.StatusCode switch
                {
                    HttpStatusCode.OK => await GetServiceResult(response),
                    _ => new ServiceFailed<TResult>(query.ErrorCode, $"{response.StatusCode}")
                };
                return serviceResult;
            }
            catch (Exception ex)
            {
                return new ServiceFailed<TResult>(query.ErrorCode, ex.Message);
            }

            async Task<ServiceResult<TResult>> GetServiceResult(HttpResponseMessage response)
            {
                var content = await response.Content.ReadAsStringAsync();
                return _serializer.Deserialize(content, typeof(ServiceResult<TResult>)) as ServiceResult<TResult>;
            }
        }
    }
}
