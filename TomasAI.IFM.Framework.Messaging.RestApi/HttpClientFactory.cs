using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace TomasAI.IFM.Framework.Messaging.RestApi;

/// <summary>
/// Provides a factory for creating <see cref="HttpClient"/> instances using dependency injection.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HttpClientFactory"/> class.
/// </remarks>
/// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> used to create <see cref="HttpClient"/> instances.</param>
public class HttpClientFactory(IHttpClientFactory httpClientFactory)
{
    readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> instance using the default configuration.
    /// </summary>
    /// <returns>A new <see cref="HttpClient"/> instance.</returns>
    public HttpClient CreateClient()
    {
        return _httpClientFactory.CreateClient();
    }

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> instance with the specified name.
    /// </summary>
    /// <param name="name">The logical name of the <see cref="HttpClient"/> to create.</param>
    /// <returns>A new <see cref="HttpClient"/> instance configured with the specified name.</returns>
    public HttpClient CreateClient(string name)
    {
        return _httpClientFactory.CreateClient(name);
    }
}
