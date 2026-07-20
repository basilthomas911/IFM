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

    public class CommandServiceRestApiClient : ICommandService
    {
        readonly Uri _baseUri;
        readonly HttpClient _httpClient;
       // readonly IHttpClientFactory _httpClientFactory;
        readonly IJsonSerializer _serializer;

        /// <summary>
        /// create command service rest api client
        /// </summary>
        /// <param name="options"></param>
        /// <param name="serializer"></param>
        public CommandServiceRestApiClient(ICommandServiceRestApiOptions options, IJsonSerializer serializer)
        {
            IsArgumentNull.Check(options.BaseUri);
            _serializer = IsArgumentNull.Set(serializer);
            //_httpClientFactory = IsArgumentNull.Set(httpClientFactory);
            _baseUri = new Uri(options.BaseUri);
            _httpClient = new HttpClient(new HttpClientHandler { UseDefaultCredentials = true });
            _httpClient.Timeout = TimeSpan.FromMinutes(10);
        }

        /// <summary>
        /// execute command asynchronously and return service result
        /// </summary>
        /// <param name="command"></param>
        /// <param name="controllerName"></param>
        /// <returns></returns>
        public async Task<ServiceResult<Guid>> ExecuteApiCommandAsync(ICommand command, string controllerName)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(command);
                ArgumentNullException.ThrowIfNull(controllerName);

                var commandName = command.GetType().Name.Replace("Command", "");
                var commandUri = $"api/{controllerName}/{commandName}";
                var serializedContent = _serializer.Serialize(command);
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(_baseUri, commandUri),
                    Content = new StringContent(serializedContent, Encoding.UTF8, "application/json"),
                };
                request.Headers.Add("X-CommandTypeName", command.GetType().AssemblyQualifiedName);
                request.Headers.Add("Cache-Control", "max-age=0, no-cache, must-revalidate, proxy-revalidate");
                //using HttpClient httpClient = _httpClientFactory.CreateClient("HttpRestApi");
                var response = await _httpClient.SendAsync(request);
                var serviceResult = response.StatusCode switch
                {
                    HttpStatusCode.OK => await GetServiceResult(response),
                    HttpStatusCode.InternalServerError => await GetServiceFailed(response),
                    _ => await GetFatalError(response)
                };
                return serviceResult;
            }
            catch (Exception ex)
            {
                return new ServiceFailed<Guid>(command.ErrorCode, ex.Message);
            }

            async Task<ServiceResult<Guid>> GetServiceResult(HttpResponseMessage response)
            {
                var content = await response.Content.ReadAsStringAsync();
                return (_serializer.Deserialize(content, typeof(ServiceResult<Guid>)) as ServiceResult<Guid>)!;
            }

            async Task<ServiceResult<Guid>> GetServiceFailed(HttpResponseMessage response)
            {
                var content = await response.Content.ReadAsStringAsync();
                return (_serializer.Deserialize(content, typeof(ServiceFailed<Guid>)) as ServiceResult<Guid>)!;
            }

            async Task<ServiceResult<Guid>> GetFatalError(HttpResponseMessage response)
            {
                var errorMsg = string.Empty;
                var content = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(content))
                {
                    content = response.ReasonPhrase;
                    errorMsg = $"{content} - {response?.RequestMessage?.RequestUri}";
                    return new ServiceFailed<Guid>(command.ErrorCode, errorMsg);
                }
                return (_serializer.Deserialize(content, typeof(ServiceResult<Guid>)) as ServiceResult<Guid>)!;
            }


        }


        public async Task<ServiceResult<Guid>> PostApiCommandAsync(ICommand command, string controllerName)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(command);

                var commandName = command.GetType().Name.ToLower().Replace("command", "");
                var commandUri = $"api/{controllerName}/{commandName}";
                var serializedContent = _serializer.Serialize(command);
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(_baseUri, commandUri),
                    Content = new StringContent(serializedContent, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("X-CommandTypeName", command.GetType().AssemblyQualifiedName);
                var response = await _httpClient.SendAsync(request);
                var serviceResult = response.StatusCode switch
                {
                    HttpStatusCode.OK => await GetServiceResult(response),
                    _ => new ServiceFailed<Guid>(command.ErrorCode, $"{response.StatusCode}")
                };
                return serviceResult;
            }
            catch (Exception ex)
            {
                return new ServiceFailed<Guid>(command.ErrorCode, ex.Message);
            }

            async Task<ServiceResult<Guid>> GetServiceResult(HttpResponseMessage response)
            {
                var content = await response.Content.ReadAsStringAsync();
                return _serializer.Deserialize(content, typeof(ServiceResult<Guid>)) as ServiceResult<Guid>;
            }

        }

    }

}
