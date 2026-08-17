using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

public static class InfrastructureProbe
{
    public static async Task ProbeTcpAsync(
        G0Endpoint endpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        using TcpClient client = new();
        try
        {
            await client.ConnectAsync(endpoint.Host, endpoint.Port, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            throw new G0DependencyException(
                $"{endpoint.Name} did not accept TCP connections at {endpoint.Host}:{endpoint.Port}: {exception.Message}");
        }
    }

    public static async Task<ApiReadinessDocument> WaitForApiReadinessAsync(
        Uri readinessUri,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Func<(bool HasExited, int? ExitCode)>? processStatus = null)
    {
        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(5) };
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        Exception? lastFailure = null;
        while (!timeoutSource.IsCancellationRequested)
        {
            var status = processStatus?.Invoke();
            if (status is { HasExited: true })
                throw new InvalidOperationException(
                    $"API process exited before readiness with code {status.Value.ExitCode?.ToString() ?? "unknown"}.");
            try
            {
                using var response = await client.GetAsync(readinessUri, timeoutSource.Token).ConfigureAwait(false);
                var json = await response.Content.ReadAsStringAsync(timeoutSource.Token).ConfigureAwait(false);
                var document = JsonSerializer.Deserialize<ApiReadinessDocument>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (response.IsSuccessStatusCode && document is not null)
                    return document;
                lastFailure = new InvalidOperationException(
                    $"Readiness returned {(int)response.StatusCode}: {json}");
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                lastFailure = exception;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        throw new TimeoutException(
            $"API readiness at {readinessUri} was not healthy within {timeout}.",
            lastFailure);
    }

    public static async Task<IReadOnlyList<TcpConnectionEvidence>> GetProcessTcpConnectionsAsync(
        int processId,
        CancellationToken cancellationToken)
        => (await GetTcpConnectionsAsync(cancellationToken).ConfigureAwait(false))
            .Where(row => row.ProcessId == processId)
            .ToArray();

    public static async Task<IReadOnlyList<TcpConnectionEvidence>> GetPortTcpConnectionsAsync(
        int port,
        CancellationToken cancellationToken)
        => (await GetTcpConnectionsAsync(cancellationToken).ConfigureAwait(false))
            .Where(row => GetPort(row.LocalEndpoint) == port || GetPort(row.RemoteEndpoint) == port)
            .ToArray();

    static async Task<IReadOnlyList<TcpConnectionEvidence>> GetTcpConnectionsAsync(
        CancellationToken cancellationToken)
    {
        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = "netstat.exe",
            Arguments = "-ano -p TCP",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Could not start netstat.exe.");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"netstat exited with {process.ExitCode}: {error}");

        List<TcpConnectionEvidence> connections = [];
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var columns = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length < 5
                || !string.Equals(columns[0], "TCP", StringComparison.OrdinalIgnoreCase)
                || !int.TryParse(columns[^1], out var rowProcessId))
                continue;

            connections.Add(new TcpConnectionEvidence(
                columns[1],
                columns[2],
                columns[3],
                rowProcessId));
        }

        return connections;
    }

    public static int GetPort(string endpoint)
    {
        var closeBracket = endpoint.LastIndexOf(']');
        var separator = endpoint.LastIndexOf(':');
        if (separator <= closeBracket || !int.TryParse(endpoint[(separator + 1)..], out var port))
            return -1;
        return port;
    }
}

public sealed class ApiReadinessDocument
{
    public string Status { get; init; } = string.Empty;
    public double TotalDurationMilliseconds { get; init; }
    public Dictionary<string, ApiReadinessEntry> Entries { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public int? RegisteredActorTypes
        => Entries.TryGetValue("actor_runtime", out var entry)
           && entry.Data.TryGetValue("registeredActorTypes", out var value)
           && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;
}

public sealed class ApiReadinessEntry
{
    public string Status { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public double DurationMilliseconds { get; init; }
    public Dictionary<string, JsonElement> Data { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record TcpConnectionEvidence(
    string LocalEndpoint,
    string RemoteEndpoint,
    string State,
    int ProcessId);
