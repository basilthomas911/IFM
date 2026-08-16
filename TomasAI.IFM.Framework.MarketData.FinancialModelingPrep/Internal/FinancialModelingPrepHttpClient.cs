using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

internal sealed class FinancialModelingPrepHttpClient
{
    private static readonly Meter Meter = new("TomasAI.IFM.Framework.MarketData.FMP");
    private static readonly Counter<long> RequestCount = Meter.CreateCounter<long>("ifm.fmp.http.requests");
    private static readonly Counter<long> RetryCount = Meter.CreateCounter<long>("ifm.fmp.http.retries");
    private static readonly Counter<long> ResponseBytes = Meter.CreateCounter<long>("ifm.fmp.http.response.bytes");
    private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>("ifm.fmp.http.duration.ms");
    private static readonly HttpStatusCode[] TransientStatusCodes =
    [
        HttpStatusCode.RequestTimeout,
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout
    ];

    private readonly HttpClient _httpClient;
    private readonly FinancialModelingPrepOptions _options;
    private readonly FinancialModelingPrepRequestGate _requestGate;
    private readonly TimeProvider _timeProvider;

    public FinancialModelingPrepHttpClient(
        HttpClient httpClient,
        FinancialModelingPrepOptions options,
        FinancialModelingPrepRequestGate requestGate,
        TimeProvider timeProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _requestGate = requestGate ?? throw new ArgumentNullException(nameof(requestGate));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        _options.Validate(requireApiKey: false);
        _httpClient.BaseAddress = _options.BaseAddress;
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<byte[]> GetAsync(string relativeUri, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeUri);
        _requestGate.ThrowIfCircuitOpen(_timeProvider);
        var dataset = DatasetTag(relativeUri);

        for (var attempt = 0; attempt <= _options.MaximumRetryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TimeSpan? retryDelay = null;

            try
            {
                var requestStarted = Stopwatch.GetTimestamp();
                RequestCount.Add(1, new KeyValuePair<string, object?>("dataset", dataset));
                using var lease = await _requestGate.EnterAsync(cancellationToken).ConfigureAwait(false);
                using var request = new HttpRequestMessage(HttpMethod.Get, relativeUri);
                request.Headers.TryAddWithoutValidation("apikey", _options.GetApiKey());
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                requestTimeout.CancelAfter(_options.RequestTimeout);

                using var response = await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        requestTimeout.Token)
                    .ConfigureAwait(false);
                RequestDuration.Record(
                    Stopwatch.GetElapsedTime(requestStarted).TotalMilliseconds,
                    new KeyValuePair<string, object?>("dataset", dataset),
                    new KeyValuePair<string, object?>("status_class", $"{(int)response.StatusCode / 100}xx"));

                if (IsTransient(response.StatusCode) && attempt < _options.MaximumRetryAttempts)
                {
                    retryDelay = GetRetryDelay(response.Headers.RetryAfter, attempt);
                }
                else
                {
                    EnsureSuccess(response.StatusCode);
                    var payload = await ReadBoundedAsync(response.Content, cancellationToken).ConfigureAwait(false);
                    ResponseBytes.Add(payload.Length, new KeyValuePair<string, object?>("dataset", dataset));
                    _requestGate.RecordSuccess();
                    return payload;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException exception) when (attempt >= _options.MaximumRetryAttempts)
            {
                _requestGate.RecordTransientFailure(_options, _timeProvider);
                throw new FinancialModelingPrepUnavailableException("The FMP request exceeded its bounded timeout.", exception);
            }
            catch (OperationCanceledException)
            {
                retryDelay = GetRetryDelay(retryAfter: null, attempt);
            }
            catch (HttpRequestException exception) when (attempt >= _options.MaximumRetryAttempts)
            {
                _requestGate.RecordTransientFailure(_options, _timeProvider);
                throw new FinancialModelingPrepUnavailableException("FMP was unavailable after bounded retries.", exception);
            }
            catch (HttpRequestException)
            {
                retryDelay = GetRetryDelay(retryAfter: null, attempt);
            }

            if (retryDelay is { } delay && delay > TimeSpan.Zero)
            {
                RetryCount.Add(1, new KeyValuePair<string, object?>("dataset", dataset));
                await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new UnreachableException();
    }

    private async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > _options.MaximumResponseBytes)
        {
            throw new FinancialModelingPrepResponseTooLargeException(_options.MaximumResponseBytes);
        }

        await using var input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream(Math.Min(_options.MaximumResponseBytes, 64 * 1024));
        var buffer = new byte[16 * 1024];

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return output.ToArray();
            }

            if (output.Length + read > _options.MaximumResponseBytes)
            {
                throw new FinancialModelingPrepResponseTooLargeException(_options.MaximumResponseBytes);
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private void EnsureSuccess(HttpStatusCode statusCode)
    {
        if ((int)statusCode is >= 200 and <= 299)
        {
            return;
        }

        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new FinancialModelingPrepAuthenticationException(statusCode);
        }

        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            _requestGate.RecordTransientFailure(_options, _timeProvider);
            throw new FinancialModelingPrepRateLimitException(statusCode);
        }

        if (IsTransient(statusCode))
        {
            _requestGate.RecordTransientFailure(_options, _timeProvider);
            throw new FinancialModelingPrepUnavailableException(
                $"FMP remained unavailable with HTTP status {(int)statusCode} after bounded retries.");
        }

        throw new FinancialModelingPrepResponseException($"FMP returned unexpected HTTP status {(int)statusCode}.");
    }

    private TimeSpan GetRetryDelay(RetryConditionHeaderValue? retryAfter, int attempt)
    {
        TimeSpan delay;
        if (retryAfter?.Delta is { } delta)
        {
            delay = delta;
        }
        else if (retryAfter?.Date is { } date)
        {
            delay = date - _timeProvider.GetUtcNow();
        }
        else
        {
            var multiplier = Math.Pow(2, attempt);
            var baseMilliseconds = _options.InitialRetryDelay.TotalMilliseconds * multiplier;
            var jitter = baseMilliseconds == 0 ? 0 : Random.Shared.NextDouble() * baseMilliseconds * 0.25;
            delay = TimeSpan.FromMilliseconds(baseMilliseconds + jitter);
        }

        if (delay < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return delay > _options.MaximumRetryDelay ? _options.MaximumRetryDelay : delay;
    }

    private static bool IsTransient(HttpStatusCode statusCode) => TransientStatusCodes.Contains(statusCode);

    private string DatasetTag(string relativeUri)
    {
        var pathLength = relativeUri.IndexOf('?');
        var path = (pathLength < 0 ? relativeUri : relativeUri[..pathLength]).TrimStart('/');
        return string.Equals(path, _options.TreasuryRatesEndpoint.TrimStart('/'), StringComparison.Ordinal)
            ? "treasury"
            : "economic-calendar";
    }

    public static string BuildDateRangeUri(string endpoint, DateOnly fromInclusive, DateOnly toInclusive) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{endpoint.TrimStart('/')}?from={fromInclusive:yyyy-MM-dd}&to={toInclusive:yyyy-MM-dd}");
}
