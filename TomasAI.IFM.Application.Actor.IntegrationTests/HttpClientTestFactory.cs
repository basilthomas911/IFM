using Microsoft.AspNetCore.Mvc.Testing;

namespace TomasAI.IFM.Application.Actor.IntegrationTests;

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
    HttpClient _httpClient = default!;

    /// <summary>
    /// Creates and returns an instance of <see cref="HttpClient"/>. If a client has already been created, the existing
    /// instance is returned.
    /// </summary>
    /// <remarks>This method ensures that only one instance of <see cref="HttpClient"/> is created and reused.
    /// Reusing the same instance improves performance and avoids socket exhaustion.</remarks>
    /// <param name="name">An optional name for the client. This parameter is currently not used in the method.</param>
    /// <returns>An instance of <see cref="HttpClient"/>. The same instance is returned on subsequent calls.</returns>
    public HttpClient CreateClient(string name = null!)
    {
        if (_httpClient is null)
        {
            _httpClient = factory.CreateClient();
        }
        return _httpClient;
    }
}
