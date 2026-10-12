using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

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

    /// <summary>Initializes a new LatestPriceAdmissionControl instance.</summary>
    /// <param name="maximumTemporarySessionsPerDataset">The maximum concurrent temporary sessions admitted for each dataset.</param>
    /// <param name="maximumStartsPerWindow">The maximum session starts permitted during each admission window.</param>
    /// <param name="startWindow">The duration of the session-start admission window.</param>
    /// <param name="clock">The monotonic clock used for admission rate and timeout accounting.</param>
    internal LatestPriceAdmissionControl(
        int maximumTemporarySessionsPerDataset = 1,
        int maximumStartsPerWindow = 5,
        TimeSpan? startWindow = null,
        ILatestPriceAdmissionClock? clock = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTemporarySessionsPerDataset);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumStartsPerWindow);

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
        (first <= second) switch { true => (first <= third ? first : third), false => (second <= third ? second : third) };

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

    /// <summary>Initializes a new AdmissionLease instance.</summary>
    /// <param name="owner">The admission controller that receives the permit on disposal.</param>
    /// <param name="dataset">The Databento dataset identifier.</param>
    private sealed class AdmissionLease(
        LatestPriceAdmissionControl owner,
        string dataset) : IDisposable
    {
        private LatestPriceAdmissionControl? _owner = owner;

        /// <summary>Releases the resources owned by this instance.</summary>
        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Release(dataset);
        }
    }
}
