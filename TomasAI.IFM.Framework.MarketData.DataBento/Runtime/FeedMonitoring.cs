namespace TomasAI.IFM.Framework.MarketData.DataBento;

public enum FeedMetricSeverity : byte
{
    Healthy = 0,
    Warning = 1,
    Critical = 2
}

public sealed record FeedMetricsSnapshot
{
    public required DateTimeOffset ObservedAt { get; init; }
    public required FeedHealthSnapshot Health { get; init; }
    public required FeedMetricSeverity Severity { get; init; }
    public required IReadOnlyList<string> Conditions { get; init; }
    public required FeedReadinessState Readiness { get; init; }
    public bool EntryGateOpen => Readiness == FeedReadinessState.Ready;
}

public interface IDatabentoFeedMetricsExporter
{
    void Export(FeedMetricsSnapshot snapshot);
}

public interface IDatabentoFeedAlertSink
{
    void Alert(FeedMetricsSnapshot snapshot);
}

public sealed class DatabentoFeedMonitor : IDisposable
{
    private static readonly TimeSpan RepeatedAlertInterval = TimeSpan.FromMinutes(1);
    private readonly Func<FeedHealthSnapshot> _health;
    private readonly FeedTransportHealthOptions _options;
    private readonly IDatabentoFeedMetricsExporter _exporter;
    private readonly IDatabentoFeedAlertSink _alerts;
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private Thread? _thread;
    private bool _stopping;
    private DateTimeOffset _lastExportAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastAlertAt = DateTimeOffset.MinValue;
    private string _lastAlertKey = string.Empty;
    private int _consecutiveHighChannelPolls;
    private int _consecutiveDrainLimitPolls;
    private ulong _priorDrainLimitHits;
    private FeedMetricsSnapshot? _latest;

    public DatabentoFeedMonitor(
        Func<FeedHealthSnapshot> health,
        FeedTransportHealthOptions options,
        IDatabentoFeedMetricsExporter exporter,
        IDatabentoFeedAlertSink alerts,
        TimeProvider? timeProvider = null)
    {
        _health = health ?? throw new ArgumentNullException(nameof(health));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        _alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        _timeProvider = timeProvider ?? TimeProvider.System;
        if (options.HealthPollInterval <= TimeSpan.Zero
            || options.MetricsExportInterval <= TimeSpan.Zero)
        {
            throw new ArgumentException("Monitoring intervals must be positive.", nameof(options));
        }
    }

    public FeedMetricsSnapshot? Latest
    {
        get
        {
            lock (_gate)
            {
                return _latest;
            }
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_thread is not null)
            {
                throw new InvalidOperationException("The feed monitor is already started.");
            }
            _stopping = false;
            _thread = new Thread(MonitorLoop)
            {
                IsBackground = true,
                Name = "Databento feed monitor",
                Priority = ThreadPriority.Normal
            };
            _thread.Start();
        }
    }

    public void Stop(TimeSpan timeout)
    {
        Thread? thread;
        lock (_gate)
        {
            _stopping = true;
            thread = _thread;
            Monitor.PulseAll(_gate);
        }
        if (thread is not null && !thread.Join(timeout))
        {
            throw new TimeoutException("The Databento feed monitor did not stop before the deadline.");
        }
        lock (_gate)
        {
            _thread = null;
        }
    }

    public FeedMetricsSnapshot PollOnce()
    {
        var now = _timeProvider.GetUtcNow();
        var snapshot = Evaluate(_health(), now);
        lock (_gate)
        {
            _latest = snapshot;
        }

        if (_lastExportAt == DateTimeOffset.MinValue
            || now - _lastExportAt >= _options.MetricsExportInterval)
        {
            _exporter.Export(snapshot);
            _lastExportAt = now;
        }

        var alertKey = string.Join('|', snapshot.Conditions);
        if (snapshot.Severity != FeedMetricSeverity.Healthy
            && (alertKey != _lastAlertKey
                || now - _lastAlertAt >= RepeatedAlertInterval))
        {
            _alerts.Alert(snapshot);
            _lastAlertKey = alertKey;
            _lastAlertAt = now;
        }
        else if (snapshot.Severity == FeedMetricSeverity.Healthy)
        {
            _lastAlertKey = string.Empty;
        }
        return snapshot;
    }

    public void Dispose()
    {
        if (_thread is not null)
        {
            Stop(TimeSpan.FromSeconds(5));
        }
    }

    private FeedMetricsSnapshot Evaluate(FeedHealthSnapshot health, DateTimeOffset now)
    {
        var conditions = new List<string>();
        var severity = FeedMetricSeverity.Healthy;
        var ringRatio = Ratio(health.RingUsedRecords, health.RingCapacityRecords);
        AddThreshold(conditions, ref severity, ringRatio, .50, .75, "native ring");

        var channelRatio = Ratio(
            unchecked((ulong)Math.Max(0, health.ChannelBatchCount)),
            unchecked((ulong)Math.Max(0, health.ChannelBatchCapacity)));
        _consecutiveHighChannelPolls = channelRatio >= .75
            ? _consecutiveHighChannelPolls + 1
            : 0;
        if (channelRatio >= .90)
        {
            Add(conditions, ref severity, FeedMetricSeverity.Critical,
                $"managed channel occupancy is {channelRatio:P0}");
        }
        else if (_consecutiveHighChannelPolls >= 2)
        {
            Add(conditions, ref severity, FeedMetricSeverity.Warning,
                $"managed channel occupancy is {channelRatio:P0} for consecutive polls");
        }

        if (health.PoolBatchCapacity > 0 && health.PoolFreeBatchCount == 0)
        {
            Add(conditions, ref severity, FeedMetricSeverity.Critical,
                "managed batch pool has no free batches");
        }
        else if (health.PoolBatchCapacity > 0 && health.PoolFreeBatchCount <= 2)
        {
            Add(conditions, ref severity, FeedMetricSeverity.Warning,
                $"managed batch pool has {health.PoolFreeBatchCount} free batches");
        }

        if (health.MaximumChannelFullWait > TimeSpan.FromMilliseconds(100))
        {
            Add(conditions, ref severity, FeedMetricSeverity.Critical,
                $"managed channel full wait reached {health.MaximumChannelFullWait.TotalMilliseconds:F0} ms");
        }
        else if (health.ChannelFullCount > 0)
        {
            Add(conditions, ref severity, FeedMetricSeverity.Warning,
                $"managed channel has been full {health.ChannelFullCount} times");
        }

        _consecutiveDrainLimitPolls = health.DrainPassLimitHitCount > _priorDrainLimitHits
            ? _consecutiveDrainLimitPolls + 1
            : 0;
        _priorDrainLimitHits = health.DrainPassLimitHitCount;
        if (_consecutiveDrainLimitPolls >= 4)
        {
            Add(conditions, ref severity, FeedMetricSeverity.Warning,
                "managed drain reached its pass limit in four consecutive polls");
        }

        if (health.State == FeedState.Faulted || health.TerminalStatus != DatabentoFeedStatus.Ok)
        {
            Add(conditions, ref severity, FeedMetricSeverity.Critical,
                $"feed fault: {health.TerminalStatus}");
        }
        if (!string.IsNullOrWhiteSpace(health.Warning))
        {
            Add(conditions, ref severity, FeedMetricSeverity.Warning, health.Warning);
        }

        var readiness = severity == FeedMetricSeverity.Critical
            ? FeedReadinessState.Suspect
            : health.TradingReady
                ? FeedReadinessState.Ready
                : FeedReadinessState.Closed;
        return new FeedMetricsSnapshot
        {
            ObservedAt = now,
            Health = health,
            Severity = severity,
            Conditions = conditions.AsReadOnly(),
            Readiness = readiness
        };
    }

    private void MonitorLoop()
    {
        while (true)
        {
            lock (_gate)
            {
                if (_stopping)
                {
                    return;
                }
            }
            PollOnce();
            lock (_gate)
            {
                if (_stopping)
                {
                    return;
                }
                Monitor.Wait(_gate, _options.HealthPollInterval);
            }
        }
    }

    private static double Ratio(ulong value, ulong capacity) =>
        capacity == 0 ? 0 : (double)value / capacity;

    private static void AddThreshold(
        List<string> conditions,
        ref FeedMetricSeverity severity,
        double ratio,
        double warning,
        double critical,
        string name)
    {
        if (ratio >= critical)
        {
            Add(conditions, ref severity, FeedMetricSeverity.Critical,
                $"{name} occupancy is {ratio:P0}");
        }
        else if (ratio >= warning)
        {
            Add(conditions, ref severity, FeedMetricSeverity.Warning,
                $"{name} occupancy is {ratio:P0}");
        }
    }

    private static void Add(
        List<string> conditions,
        ref FeedMetricSeverity severity,
        FeedMetricSeverity candidate,
        string condition)
    {
        conditions.Add(condition);
        if (candidate > severity)
        {
            severity = candidate;
        }
    }
}
