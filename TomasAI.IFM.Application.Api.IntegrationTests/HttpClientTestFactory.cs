using Microsoft.AspNetCore.Mvc.Testing;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

/// <summary>
/// Test factory for creating HttpClient instances using WebApplicationFactory.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HttpClientTestFactory"/> class.
/// </remarks>
/// <param name="factory">The WebApplicationFactory for the test server.</param>
public class HttpClientTestFactory(WebApplicationFactory<Program> factory)
    : IHttpClientFactory
{

    /// <summary>
    /// Creates a new HttpClient instance using the test factory.
    /// </summary>
    /// <param name="name">The name of the client (ignored).</param>
    /// <returns>A new HttpClient instance.</returns>
    public HttpClient CreateClient(string name = null!)
    {
        return factory.CreateClient();
    }
}
