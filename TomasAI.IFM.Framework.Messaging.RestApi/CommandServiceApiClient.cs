using System.Net;
using System.Text;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Framework.Messaging.RestApi;

/// <summary>
/// create command service rest api client
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="serializer"></param>
/// <param name="options"></param>
public class CommandServiceApiClient(IHttpClientFactory httpClientFactory, IJsonSerializer serializer, ICommandServiceApiOptions options) : ICommandServiceApi
{
    readonly Uri _baseUri = !string.IsNullOrEmpty(options.BaseUri) ? new Uri(options.BaseUri) : default!;
    readonly IHttpClientFactory _httpClientFactory = IsArgumentNull.Set(httpClientFactory);
    readonly IJsonSerializer _serializer = IsArgumentNull.Set(serializer);
    HttpClient _httpClient = default!;

    /// <summary>
    /// execute command asynchronously and return service result
    /// </summary>
    /// <param name="commandUri"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ExecuteCommandAsync(string commandUri, ICommand command)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(command);
            if (_httpClient is null)
            {
                _httpClient = _httpClientFactory.CreateClient();
                try { _httpClient.Timeout = TimeSpan.FromMinutes(10); }
                catch
                { // ignore }
                }
            }
            var response = default(HttpResponseMessage);
            var serializedContent = _serializer.Serialize(command);
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_baseUri, commandUri),
                Content = new StringContent(serializedContent, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-CommandTypeName", command.GetType().AssemblyQualifiedName);
            request.Headers.Add("Cache-Control", "max-age=0, no-cache, must-revalidate, proxy-revalidate");
            response = await _httpClient.SendAsync(request);
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

    /// <summary>
    /// Executes a command asynchronously by sending it to the specified command endpoint and returns the result of the
    /// operation.
    /// </summary>
    /// <remarks>The method serializes the command parameter as JSON and sends it via an HTTP POST request.
    /// The request includes additional headers to specify the command type. The operation may take up to 10 minutes to
    /// complete, depending on the configured HTTP client timeout. If the service returns an error or an unexpected
    /// status code, the result will indicate failure with relevant error details.</remarks>
    /// <param name="commandUri">The relative URI of the command endpoint to which the command will be sent.</param>
    /// <param name="command">The command parameter object containing the data to be serialized and sent. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a ServiceResult<Guid> indicating the
    /// outcome of the command execution. If successful, the result contains the identifier returned by the service;
    /// otherwise, it contains error information.</returns>
    public async Task<ServiceResult<Guid>> ExecuteCommandAsync(string commandUri, ICommandParameter command)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(command);
            if (_httpClient is null)
            {
                _httpClient = _httpClientFactory.CreateClient();
                try { _httpClient.Timeout = TimeSpan.FromMinutes(10); }
                catch
                { // ignore }
                }
            }
            var response = default(HttpResponseMessage);
            var serializedContent = _serializer.Serialize(command);
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_baseUri, commandUri),
                Content = new StringContent(serializedContent, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-CommandParameterTypeName", command.GetType().AssemblyQualifiedName);
            request.Headers.Add("Cache-Control", "max-age=0, no-cache, must-revalidate, proxy-revalidate");
            response = await _httpClient.SendAsync(request);
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

    /// <summary>
    /// Posts a command to the specified command URI.
    /// </summary>
    /// <param name="commandUri"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> PostCommandAsync(string commandUri, ICommand command)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(command);
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
