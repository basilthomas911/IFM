using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal delegate LatestPriceResult64 LatestPriceQueryInvoker(
    string dataset,
    LatestPriceRequest request,
    uint timeoutMilliseconds);

internal sealed class DatabentoLatestPriceClient : IDatabentoLatestPriceClient
{
    private readonly string _dataset;
    private readonly LatestPriceAdmissionControl _admissionControl;
    private readonly LatestPriceQueryInvoker _query;

    internal DatabentoLatestPriceClient(
        string dataset,
        LatestPriceAdmissionControl admissionControl,
        LatestPriceQueryInvoker? query = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
        _dataset = dataset;
        _admissionControl = admissionControl
            ?? throw new ArgumentNullException(nameof(admissionControl));
        _query = query ?? QueryNative;
    }

    public LatestPriceResult64 GetLatestPrice(
        LatestPriceRequest request,
        TimeSpan timeout)
    {
        Validate(request, timeout);
        var deadline = new MonotonicDeadline(timeout);
        using var admission = _admissionControl.Acquire(
            _dataset,
            RequireRemaining(deadline, "Latest-price admission timed out."));
        var remainingMilliseconds = deadline.RemainingMilliseconds;
        if (remainingMilliseconds == 0)
        {
            throw new DatabentoFeedTimeoutException(
                "Latest-price admission consumed the request timeout.");
        }
        var result = _query(_dataset, request, remainingMilliseconds);
        ValidateResult(request.PricePolicy, result);
        return result;
    }

    private void Validate(LatestPriceRequest request, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Dataset);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Symbol);
        if (!string.Equals(request.Dataset, _dataset, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Latest-price request dataset '{request.Dataset}' does not match "
                + $"the client dataset '{_dataset}'.",
                nameof(request));
        }
        if (request.InputSymbology is not (
                DatabentoInputSymbology.RawSymbol
                or DatabentoInputSymbology.InstrumentId))
        {
            throw new ArgumentException(
                "Latest-price input symbology is invalid.", nameof(request));
        }
        if (request.PricePolicy is not (
                LatestPricePolicy.LastTrade
                or LatestPricePolicy.QuoteMidpoint
                or LatestPricePolicy.Bid
                or LatestPricePolicy.Ask))
        {
            throw new ArgumentException(
                "Latest-price selection policy is invalid.", nameof(request));
        }
        if (request.FreshnessPolicy == LatestPriceFreshnessPolicy.NextObserved)
        {
            if (request.ReplayLookback != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "NextObserved requires a zero replay lookback.", nameof(request));
            }
        }
        else if (request.FreshnessPolicy
                 == LatestPriceFreshnessPolicy.ReplayLookbackThenLive)
        {
            ToMilliseconds(
                request.ReplayLookback,
                nameof(request),
                "ReplayLookback must be positive and less than uint.MaxValue milliseconds.");
        }
        else
        {
            throw new ArgumentException(
                "Latest-price freshness policy is invalid.", nameof(request));
        }
        ToMilliseconds(
            timeout,
            nameof(timeout),
            "The latest-price timeout must be positive and less than uint.MaxValue milliseconds.");
    }

    private static void ValidateResult(
        LatestPricePolicy requestedPolicy,
        LatestPriceResult64 result)
    {
        if (result.InstrumentId == 0 || result.SelectedPolicy != requestedPolicy)
        {
            throw new DatabentoFeedException(
                DatabentoFeedStatus.AbiMismatch,
                "The native latest-price result did not identify the requested policy and instrument.");
        }
        var qualifying = requestedPolicy switch
        {
            LatestPricePolicy.LastTrade => result.HasLastTrade,
            LatestPricePolicy.QuoteMidpoint => result.HasBid && result.HasAsk
                && result.BidPrice <= result.AskPrice,
            LatestPricePolicy.Bid => result.HasBid,
            LatestPricePolicy.Ask => result.HasAsk,
            _ => false
        };
        if (!qualifying)
        {
            throw new DatabentoFeedException(
                DatabentoFeedStatus.AbiMismatch,
                $"The native latest-price result did not contain a qualifying {requestedPolicy} value.");
        }
    }

    private static TimeSpan RequireRemaining(
        MonotonicDeadline deadline,
        string message)
    {
        var remaining = deadline.Remaining;
        if (remaining <= TimeSpan.Zero)
        {
            throw new DatabentoFeedTimeoutException(message);
        }
        return remaining;
    }

    private static uint ToMilliseconds(
        TimeSpan value,
        string parameterName,
        string message)
    {
        if (value <= TimeSpan.Zero || value.TotalMilliseconds >= uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(parameterName, message);
        }
        return checked((uint)Math.Ceiling(value.TotalMilliseconds));
    }

    private static unsafe LatestPriceResult64 QueryNative(
        string dataset,
        LatestPriceRequest request,
        uint timeoutMilliseconds)
    {
        var datasetBytes = Encoding.UTF8.GetBytes(dataset);
        var symbolBytes = Encoding.UTF8.GetBytes(request.Symbol);
        var utf8Blob = new byte[checked(datasetBytes.Length + symbolBytes.Length)];
        datasetBytes.CopyTo(utf8Blob, 0);
        symbolBytes.CopyTo(utf8Blob, datasetBytes.Length);
        fixed (byte* blobPointer = utf8Blob)
        {
            var nativeRequest = new NativeLatestPriceRequest
            {
                StructSize = checked((uint)Marshal.SizeOf<NativeLatestPriceRequest>()),
                AbiVersion = NativeConstants.AbiVersion,
                SelectedPolicy = request.PricePolicy,
                FreshnessPolicy = request.FreshnessPolicy,
                InputSymbology = request.InputSymbology,
                ReplayLookbackMilliseconds = request.FreshnessPolicy
                    == LatestPriceFreshnessPolicy.NextObserved
                        ? 0
                        : ToMilliseconds(
                            request.ReplayLookback,
                            nameof(request),
                            "ReplayLookback is outside the native range."),
                Dataset = new NativeUtf8Slice
                {
                    Offset = 0,
                    Length = checked((uint)datasetBytes.Length)
                },
                Symbol = new NativeUtf8Slice
                {
                    Offset = checked((uint)datasetBytes.Length),
                    Length = checked((uint)symbolBytes.Length)
                },
                Utf8Blob = blobPointer,
                Utf8BlobBytes = checked((uint)utf8Blob.Length)
            };
            var status = NativeMethods.GetLatestPrice(
                &nativeRequest,
                timeoutMilliseconds,
                out var result);
            NativeStatus.ThrowIfFailed(status, null, "Get latest price");
            return result;
        }
    }
}

internal interface ILatestPriceAdmissionClock
{
    long GetTimestamp();
    TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp);
    void Wait(object gate, TimeSpan timeout);
}

internal sealed class SystemLatestPriceAdmissionClock : ILatestPriceAdmissionClock
{
    internal static SystemLatestPriceAdmissionClock Instance { get; } = new();

    private SystemLatestPriceAdmissionClock()
    {
    }

    public long GetTimestamp() => Stopwatch.GetTimestamp();

    public TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp) =>
        Stopwatch.GetElapsedTime(startingTimestamp, endingTimestamp);

    public void Wait(object gate, TimeSpan timeout) => Monitor.Wait(gate, timeout);
}

internal sealed class LatestPriceAdmissionControl
{
    private static readonly TimeSpan MaximumMonitorWait = TimeSpan.FromDays(1);
    internal static LatestPriceAdmissionControl Shared { get; } = new();

    private readonly object _gate = new();
    private readonly Dictionary<string, int> _activeByDataset =
        new(StringComparer.Ordinal);
    private readonly Queue<long> _connectionStarts = new();
    private readonly int _maximumTemporarySessionsPerDataset;
    private readonly int _maximumStartsPerWindow;
    private readonly TimeSpan _startWindow;
    private readonly ILatestPriceAdmissionClock _clock;

    internal LatestPriceAdmissionControl(
        int maximumTemporarySessionsPerDataset = 1,
        int maximumStartsPerWindow = 5,
        TimeSpan? startWindow = null,
        ILatestPriceAdmissionClock? clock = null)
    {
        if (maximumTemporarySessionsPerDataset <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumTemporarySessionsPerDataset));
        }
        if (maximumStartsPerWindow <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumStartsPerWindow));
        }
        _startWindow = startWindow ?? TimeSpan.FromSeconds(1);
        if (_startWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(startWindow));
        }
        _maximumTemporarySessionsPerDataset = maximumTemporarySessionsPerDataset;
        _maximumStartsPerWindow = maximumStartsPerWindow;
        _clock = clock ?? SystemLatestPriceAdmissionClock.Instance;
    }

    internal IDisposable Acquire(string dataset, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
        if (timeout <= TimeSpan.Zero)
        {
            throw new DatabentoFeedTimeoutException(
                "Latest-price admission had no remaining timeout.");
        }
        var started = _clock.GetTimestamp();
        lock (_gate)
        {
            WaitForDatasetPermit(dataset, started, timeout);
            _activeByDataset.TryGetValue(dataset, out var active);
            _activeByDataset[dataset] = active + 1;
            try
            {
                WaitForConnectionStartPermit(started, timeout);
                _connectionStarts.Enqueue(_clock.GetTimestamp());
                return new AdmissionLease(this, dataset);
            }
            catch
            {
                ReleaseDataset(dataset);
                Monitor.PulseAll(_gate);
                throw;
            }
        }
    }

    internal int GetActiveSessionCount(string dataset)
    {
        lock (_gate)
        {
            return _activeByDataset.TryGetValue(dataset, out var count) ? count : 0;
        }
    }

    private void WaitForDatasetPermit(
        string dataset,
        long started,
        TimeSpan timeout)
    {
        while (_activeByDataset.TryGetValue(dataset, out var active)
               && active >= _maximumTemporarySessionsPerDataset)
        {
            WaitWithinDeadline(
                started,
                timeout,
                timeout,
                $"The temporary latest-price session budget for '{dataset}' was not available before the timeout.");
        }
    }

    private void WaitForConnectionStartPermit(long started, TimeSpan timeout)
    {
        while (true)
        {
            var now = _clock.GetTimestamp();
            while (_connectionStarts.TryPeek(out var oldest)
                   && _clock.GetElapsedTime(oldest, now) >= _startWindow)
            {
                _connectionStarts.Dequeue();
            }
            if (_connectionStarts.Count < _maximumStartsPerWindow)
            {
                return;
            }
            var delay = _startWindow
                        - _clock.GetElapsedTime(_connectionStarts.Peek(), now);
            WaitWithinDeadline(
                started,
                timeout,
                delay,
                "The five-connections-per-second governor could not admit the latest-price request before its timeout.");
        }
    }

    private void WaitWithinDeadline(
        long started,
        TimeSpan timeout,
        TimeSpan requestedWait,
        string timeoutMessage)
    {
        var elapsed = _clock.GetElapsedTime(started, _clock.GetTimestamp());
        var remaining = timeout - elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            throw new DatabentoFeedTimeoutException(timeoutMessage);
        }
        var wait = Min(remaining, requestedWait, MaximumMonitorWait);
        _clock.Wait(_gate, wait);
        if (_clock.GetElapsedTime(started, _clock.GetTimestamp()) >= timeout)
        {
            throw new DatabentoFeedTimeoutException(timeoutMessage);
        }
    }

    private static TimeSpan Min(TimeSpan first, TimeSpan second, TimeSpan third) =>
        first <= second
            ? (first <= third ? first : third)
            : (second <= third ? second : third);

    private void Release(string dataset)
    {
        lock (_gate)
        {
            ReleaseDataset(dataset);
            Monitor.PulseAll(_gate);
        }
    }

    private void ReleaseDataset(string dataset)
    {
        var remaining = _activeByDataset[dataset] - 1;
        if (remaining == 0)
        {
            _activeByDataset.Remove(dataset);
        }
        else
        {
            _activeByDataset[dataset] = remaining;
        }
    }

    private sealed class AdmissionLease(
        LatestPriceAdmissionControl owner,
        string dataset) : IDisposable
    {
        private LatestPriceAdmissionControl? _owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Release(dataset);
        }
    }
}
