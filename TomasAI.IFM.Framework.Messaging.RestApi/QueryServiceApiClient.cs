using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Framework.Messaging.RestApi;

/// <summary>
/// Provides an API client for executing queries against a remote query service.
/// </summary>
/// <remarks>This class is designed to interact with a query service by sending HTTP requests and processing the
/// responses. It supports both GET and POST operations for executing queries, and it uses dependency-injected
/// components such as <see cref="IHttpClientFactory"/> and <see cref="IJsonSerializer"/> for HTTP communication and
/// serialization.</remarks>
public class QueryServiceApiClient : IQueryServiceApi
{
    readonly Uri _baseUri;
    readonly IJsonSerializer _serializer;
    IHttpClientFactory _httpClientFactory = default!;
    HttpClient _httpClient = default!;

    public QueryServiceApiClient(IHttpClientFactory httpClientFactory, IJsonSerializer serializer, IQueryServiceApiOptions options)
    {
        IsArgumentNull.Check(options.BaseUri);
        _serializer = IsArgumentNull.Set(serializer);
        _httpClientFactory = IsArgumentNull.Set(httpClientFactory);
        _baseUri = new Uri(options.BaseUri);
        //_httpClient = new HttpClient(new HttpClientHandler { UseDefaultCredentials = true });
    }

    public void SetHttpClientFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = IsArgumentNull.Set(httpClientFactory);
    }

    public async Task<ServiceResult<TResult>> ExecuteQueryAsync<TResult>(string uriPath, IQuery<TResult> query)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(query);
            IsArgumentNull.Check(uriPath);
            if (_httpClient is null)
            {
                _httpClient = _httpClientFactory.CreateClient();
                try { _httpClient.Timeout = TimeSpan.FromMinutes(10); }
                catch
                { // ignore }
                }
            }

            var queryUri = string.IsNullOrWhiteSpace(query.QueryParams)
                 ? uriPath
                  : $"{uriPath}?{query.QueryParams}";
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

    /// <summary>
    /// Executes an asynchronous HTTP GET request with the specified query parameters and returns the result as a
    /// service response of the specified type.
    /// </summary>
    /// <remarks>If the HTTP request is successful, the method returns a deserialized service result of the
    /// specified type. If the request fails or an exception is thrown, a failed service result is returned with the
    /// provided error code. The method sets a default timeout of 10 minutes for the HTTP client if not already
    /// configured.</remarks>
    /// <typeparam name="TResult">The type of the result expected from the service response.</typeparam>
    /// <param name="uriPath">The relative URI path to which the query will be sent. Cannot be null.</param>
    /// <param name="queryParam">An object containing the query parameters to include in the request. Cannot be null.</param>
    /// <param name="errorCode">The error code to use if the request fails or an exception occurs.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a service result of type
    /// <typeparamref name="TResult"/> representing the outcome of the query.</returns>
    public async Task<ServiceResult<TResult>> ExecuteQueryAsync<TResult>(string uriPath, IQueryParameter queryParam, int errorCode)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(queryParam);
            IsArgumentNull.Check(uriPath);
            if (_httpClient is null)
            {
                _httpClient = _httpClientFactory.CreateClient();
                try { _httpClient.Timeout = TimeSpan.FromMinutes(10); }
                catch
                { // ignore }
                }
            }

            var queryUri = string.IsNullOrWhiteSpace(queryParam.QueryParams)
                 ? uriPath
                  : $"{uriPath}?{queryParam.QueryParams}";
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(_baseUri, queryUri),
            };
            request.Headers.Add("X-QueryParameterTypeName", queryParam.GetType().AssemblyQualifiedName);
            request.Headers.Add("Cache-Control", "max-age=0, no-cache, must-revalidate, proxy-revalidate");
            var response = await _httpClient.SendAsync(request);
            var serviceResult = response.StatusCode switch
            {
                HttpStatusCode.OK => await GetServiceResult(response),
                HttpStatusCode.InternalServerError => await GetServiceFailed(response),
                _ => new ServiceFailed<TResult>(errorCode, $"{response.StatusCode} - {response!.RequestMessage!.RequestUri}")
            };
            return serviceResult;
        }
        catch (Exception ex)
        {
            return new ServiceFailed<TResult>(errorCode, ex.Message);
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

    public async Task<ServiceResult<TResult>> PostQueryAsync<TResult>(string uriPath, IQueryParameter queryParam, int errorCode)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(queryParam);
            IsArgumentNull.Check(uriPath);
            if (_httpClient is null)
            {
                _httpClient = _httpClientFactory.CreateClient();
                try { _httpClient.Timeout = TimeSpan.FromMinutes(10); }
                catch
                { // ignore }
                }
            }

            var queryUri = string.IsNullOrWhiteSpace(queryParam.QueryParams)
                ? uriPath
                : $"{uriPath}?{queryParam.QueryParams}";
            var serializedContent = _serializer.Serialize(queryParam);
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_baseUri, queryUri),
                Content = new StringContent(serializedContent, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-QueryTypeName", queryParam.GetType().AssemblyQualifiedName);
            var response = await _httpClient.SendAsync(request);
            var serviceResult = response.StatusCode switch
            {
                HttpStatusCode.OK => await GetServiceResult(response),
                _ => new ServiceFailed<TResult>(errorCode, $"{response.StatusCode}")
            };
            return serviceResult;
        }
        catch (Exception ex)
        {
            return new ServiceFailed<TResult>(errorCode, ex.Message);
        }

        async Task<ServiceResult<TResult>> GetServiceResult(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            return (_serializer.Deserialize(content, typeof(ServiceResult<TResult>)) as ServiceResult<TResult>)!;
        }
    }
}
