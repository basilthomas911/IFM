namespace TomasAI.IFM.Application.Actor.IntegrationTests;

/// <summary>
/// Test factory that lazily creates and reuses one <see cref="HttpClient"/>.
/// </summary>
/// <param name="createClient">Creates a client for the owning integration host.</param>
public sealed class HttpClientTestFactory(Func<HttpClient> createClient) : IHttpClientFactory
{
    HttpClient? httpClient;

    /// <summary>
    /// Creates the client on first use and returns the same instance thereafter.
    /// </summary>
    /// <param name="name">An optional client name; the integration host has one client configuration.</param>
    /// <returns>The client associated with the owning integration host.</returns>
    public HttpClient CreateClient(string name = null!)
        => httpClient ??= createClient();
}