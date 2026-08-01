namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class FeedMonitoringTests
{
    [Fact]
    public void PollOnce_UsesOneSecondPollStateAndFiveSecondExportCadence()
    {
        var time = new ManualTimeProvider();
        var exporter = new Collector();
        var monitor = new DatabentoFeedMonitor(
            () => HealthyHealth(),
            new FeedTransportHealthOptions(),
            exporter,
            exporter,
            time);

        monitor.PollOnce();
        for (var index = 0; index < 4; index++)
        {
            time.Advance(TimeSpan.FromSeconds(1));
            monitor.PollOnce();
        }
        Assert.Single(exporter.Exports);

        time.Advance(TimeSpan.FromSeconds(1));
        monitor.PollOnce();

        Assert.Equal(2, exporter.Exports.Count);
    }

    [Fact]
    public void PollOnce_AppliesPressureThresholdsAndClosesEntryGate()
    {
        var health = HealthyHealth() with
        {
            RingUsedRecords = 75,
            ChannelBatchCapacity = 100,
            ChannelBatchCount = 90,
            PoolBatchCapacity = 10,
            PoolFreeBatchCount = 0,
            MaximumChannelFullWait = TimeSpan.FromMilliseconds(101)
        };
        var collector = new Collector();
        var monitor = new DatabentoFeedMonitor(
            () => health,
            new FeedTransportHealthOptions(),
            collector,
            collector);

        var snapshot = monitor.PollOnce();

        Assert.Equal(FeedMetricSeverity.Critical, snapshot.Severity);
        Assert.Equal(FeedReadinessState.Suspect, snapshot.Readiness);
        Assert.False(snapshot.EntryGateOpen);
        Assert.Contains(snapshot.Conditions, x => x.Contains("native ring"));
        Assert.Contains(snapshot.Conditions, x => x.Contains("no free batches"));
    }

    [Fact]
    public void PollOnce_RateLimitsUnchangedAlertForOneMinute()
    {
        var time = new ManualTimeProvider();
        var collector = new Collector();
        var health = HealthyHealth() with { RingUsedRecords = 50 };
        var monitor = new DatabentoFeedMonitor(
            () => health,
            new FeedTransportHealthOptions(),
            collector,
            collector,
            time);

        monitor.PollOnce();
        time.Advance(TimeSpan.FromSeconds(59));
        monitor.PollOnce();
        Assert.Single(collector.Alerts);

        time.Advance(TimeSpan.FromSeconds(1));
        monitor.PollOnce();
        Assert.Equal(2, collector.Alerts.Count);
    }

    private static FeedHealthSnapshot HealthyHealth() => new(
        FeedState.Running,
        DatabentoFeedStatus.Ok,
        100,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        null)
    {
        TransportReady = true,
        TradingReady = true,
        BaselineReadyInstrumentCount = 1,
        InstrumentCount = 1,
        ChannelBatchCapacity = 100,
        PoolBatchCapacity = 10,
        PoolFreeBatchCount = 10
    };

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        internal void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class Collector : IDatabentoFeedMetricsExporter, IDatabentoFeedAlertSink
    {
        internal List<FeedMetricsSnapshot> Exports { get; } = [];
        internal List<FeedMetricsSnapshot> Alerts { get; } = [];
        public void Export(FeedMetricsSnapshot snapshot) => Exports.Add(snapshot);
        public void Alert(FeedMetricsSnapshot snapshot) => Alerts.Add(snapshot);
    }
}
