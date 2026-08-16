using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.UnitTests;

internal sealed class RecordingHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.TryGetValues("apikey", out var values);
        Requests.Enqueue(new RecordedRequest(
            request.RequestUri?.ToString() ?? string.Empty,
            values?.SingleOrDefault()));
        return responder(request, cancellationToken);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}

internal sealed record RecordedRequest(string Uri, string? ApiKeyHeader);

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

internal static class FmpTestOptions
{
    public static FinancialModelingPrepOptions Create(out string environmentVariable, string secret = "unit-test-secret")
    {
        environmentVariable = $"IFM_FMP_TEST_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(environmentVariable, secret);
        return new FinancialModelingPrepOptions
        {
            ApiKeyEnvironmentVariable = environmentVariable,
            MaximumRetryAttempts = 0,
            InitialRetryDelay = TimeSpan.Zero,
            MaximumRetryDelay = TimeSpan.Zero,
            RequestTimeout = TimeSpan.FromSeconds(2),
            TotalOperationTimeout = TimeSpan.FromSeconds(5)
        };
    }
}
