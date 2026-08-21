using System.Net.Sockets;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class DependencyProbeService(SchedulerHostOptions options, IHttpClientFactory httpClientFactory)
{
    public async Task<string?> FindBlockingDependencyAsync(
        ScheduledTaskCatalogDefinition task,
        CancellationToken cancellationToken)
    {
        foreach (var key in task.RequiredEndpoints)
        {
            var endpoint = new Uri(options.DependencyEndpoints[key], UriKind.Absolute);
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                if (endpoint.Scheme is "http" or "https")
                {
                    using var response = await httpClientFactory.CreateClient(nameof(DependencyProbeService))
                        .GetAsync(endpoint, timeout.Token);
                    if (!response.IsSuccessStatusCode)
                    {
                        return $"Dependency '{key}' returned HTTP {(int)response.StatusCode}.";
                    }
                }
                else
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync(endpoint.Host, endpoint.Port, timeout.Token);
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or SocketException or OperationCanceledException)
            {
                return $"Dependency '{key}' is unavailable: {exception.Message}";
            }
        }

        return null;
    }
}
