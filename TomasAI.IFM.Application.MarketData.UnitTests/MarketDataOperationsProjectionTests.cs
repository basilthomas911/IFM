using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.MarketData.Contracts;
using NSubstitute;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class MarketDataOperationsProjectionTests
{
    [Fact]
    public async Task Explicit_view_reads_capture_new_host_counters_without_starting_or_recovering_feeds()
    {
        var time = new ProjectionTime();
        var health = new MarketDataOperationsHealthService(new(), time);
        var api = Substitute.For<IMarketDataApi>();
        var first = NativeBackend() with
        {
            Datasets = [Dataset("Futures", "GLBX.MDP3") with { Counters = new Dictionary<string, long> { ["Trade events"] = 10 } }]
        };
        var second = first with { Datasets = [first.Datasets[0] with { Counters = new Dictionary<string, long> { ["Trade events"] = 20 } }] };
        api.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(first, second);
        var outlook = Substitute.For<IMarketOutlookOperations>();
        outlook.GetMetrics().Returns(new MarketOutlookProcessorMetricsSnapshot
        {
            IsProcessorReady = true,
            Updates = new Dictionary<MarketOutlookUpdateKind, MarketOutlookUpdateMetricSnapshot>()
        });
        var sessions = Substitute.For<IFuturesMarketSessionAuthority>();
        sessions.Current.Returns(new MarketSessionReadModel { State = FuturesMarketState.OffTrading, ActiveValueDate = first.ValueDate });
        var publisher = Substitute.For<TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation.ITickAggregationEventPublisher>();
        var observer = new MarketDataBackendHealthObserver(api, health, outlook, sessions, publisher, time,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MarketDataBackendHealthObserver>.Instance);
        await observer.ObserveOnceAsync(default);
        Assert.Equal(10, Assert.Single(health.GetReadModel().Datasets).Counters!["Trade events"]);
        await observer.ObserveOnceAsync(default);
        Assert.Equal(20, Assert.Single(health.GetReadModel().Datasets).Counters!["Trade events"]);
        Assert.Equal(2, api.ReceivedCalls().Count());
        Assert.All(api.ReceivedCalls(), call => Assert.Equal(nameof(IMarketDataApi.GetBackendStatusAsync), call.GetMethodInfo().Name));
    }

    [Fact]
    public void Native_counters_survive_messagepack_and_operations_projection()
    {
        var dataset = Dataset("Futures", "GLBX.MDP3") with
        {
            Counters = new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["Trade events"] = 12345,
                ["Publication queue capacity"] = 256,
                ["Publication retained bytes"] = 0
            }
        };
        var wire = MessagePack.MessagePackSerializer.Serialize(dataset);
        var decoded = MessagePack.MessagePackSerializer.Deserialize<MarketDataDatasetStatus>(wire);
        var time = new ProjectionTime();
        var health = new MarketDataOperationsHealthService(new(), time);
        Observe(health, time, backend: NativeBackend() with { Datasets = [decoded] });
        var row = Assert.Single(health.GetReadModel().Datasets);
        Assert.Equal("RustDataset", row.ResourceKind);
        Assert.Equal(12345, row.Counters!["Trade events"]);
        Assert.Equal(256, row.Counters["Publication queue capacity"]);
        Assert.Equal(0, row.Counters["Publication retained bytes"]);
    }

    [Fact]
    public void Native_resources_replace_legacy_stages_and_distinguish_futures_from_options()
    {
        var time = new ProjectionTime();
        var health = new MarketDataOperationsHealthService(new(), time);
        Observe(health, time, backend: NativeBackend());
        var result = health.GetReadModel();
        Assert.Equal("Green", result.OverallStatus);
        Assert.DoesNotContain(result.Stages, row => row.Stage is "DatabentoNative" or "DatabentoInterop"
            or "DatabentoAggregation" or "DatabentoWorkerProcess" or "DatabentoLifecycle"
            or "DatabentoRefresh" or "DatabentoRealtimePublication" or "UiDelivery");
        Assert.Contains(result.Stages, row => row.Stage == "DatabentoGenerationIngress");
        Assert.Contains(result.Stages, row => row.Stage == "EmaAnalytics");
        Assert.Equal(3, result.Datasets.Count);
        Assert.Equal(3, result.Datasets.Select(row => row.Dataset).Distinct(StringComparer.Ordinal).Count());
        Assert.All(result.Datasets, row =>
        {
            Assert.Equal("Green", row.Status);
            Assert.Equal(123, row.ProcessId);
            Assert.NotEqual(Guid.Empty, row.WorkerInstanceId);
            Assert.NotEqual(Guid.Empty, row.GenerationId);
            Assert.Equal(time.GetUtcNow().UtcDateTime, row.LastObservedUtc);
            Assert.Contains("Rust dataset resource", row.Reason, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("Recovering", true, false, "Orange")]
    [InlineData("Failed", false, false, "Red")]
    [InlineData("Healthy", true, false, "Red")]
    [InlineData("Stopped", false, false, "Inactive")]
    public void Native_dataset_failure_and_recovery_remain_visible(string lifecycle, bool running, bool healthy, string expected)
    {
        var time = new ProjectionTime();
        var health = new MarketDataOperationsHealthService(new(), time);
        var backend = NativeBackend();
        Observe(health, time, backend: backend);
        time.Advance(TimeSpan.FromSeconds(5));
        Observe(health, time, backend: backend with
        {
            Datasets = [backend.Datasets[0] with { Lifecycle = lifecycle, Running = running, Healthy = healthy, Error = "test reason" }]
        });
        var row = Assert.Single(health.GetReadModel().Datasets);
        Assert.Equal(expected, row.Status);
        Assert.Contains("test reason", row.Reason, StringComparison.Ordinal);
        Assert.Equal(time.GetUtcNow().UtcDateTime - TimeSpan.FromSeconds(5), row.LastHealthyUtc);
        if (expected is "Orange" or "Red") Assert.Equal(expected, health.GetReadModel().OverallStatus);
        time.Advance(TimeSpan.FromSeconds(16));
        Assert.NotEqual("Green", health.GetReadModel().OverallStatus);
    }

    [Fact]
    public void Managed_supervisor_stages_are_retained_for_process_owned_backends()
    {
        var time = new ProjectionTime();
        var health = new MarketDataOperationsHealthService(new(), time);
        Observe(health, time, backend: NativeBackend() with { RecoveryOwnership = MarketDataRecoveryOwnership.Process });
        Assert.Contains(health.GetReadModel().Stages, row => row.Stage == "DatabentoNative");
    }

    static MarketDataBackendStatus NativeBackend() => new(true, new(2026, 9, 4),
        [Dataset("Futures", "GLBX.MDP3"), Dataset("Futures", "XCBF.PITCH"), Dataset("Options", "GLBX.MDP3")],
        RecoveryOwnership: MarketDataRecoveryOwnership.TokioDatasetOwner);

    static MarketDataDatasetStatus Dataset(string role, string dataset) =>
        new(role, dataset, Guid.NewGuid(), Guid.NewGuid(), 123, true, true, "Healthy", null, new(2026, 9, 4));

    [Fact]
    public void Independent_observer_detects_blocked_outlook_despite_recent_market_source_time()
    {
        var time = new ProjectionTime();
        var health = new MarketDataOperationsHealthService(new(), time);
        health.Record(new(MarketDataOperationStage.MarketOutlookComposition, MarketDataOperationOutcome.Completed,
            MarketOutlookUpdateKind.EsTrade, Guid.NewGuid(), time.GetUtcNow().UtcDateTime,
            MarketDataAsOfUtc: time.GetUtcNow().UtcDateTime));
        Observe(health, time, pending: 1, age: TimeSpan.FromMinutes(2));
        var result = health.GetReadModel();
        Assert.Equal("Red", result.OverallStatus);
        var stage = Assert.Single(result.Stages, stage => stage.Stage == "MarketOutlookComposition");
        Assert.Equal("PendingWorkAged", stage.ReasonCode);
        Assert.Equal(time.GetUtcNow().UtcDateTime, stage.MarketDataAsOfUtc);
    }

    [Fact]
    public void Healthy_runtime_gauge_cannot_hide_recorded_processing_failure()
    {
        var time = new ProjectionTime();
        var health = new MarketDataOperationsHealthService(new(), time);
        health.Record(new(MarketDataOperationStage.MarketOutlookComposition, MarketDataOperationOutcome.Failed,
            MarketOutlookUpdateKind.EsTrade, Guid.NewGuid(), time.GetUtcNow().UtcDateTime));
        Observe(health, time);
        Assert.Equal("Red", Assert.Single(health.GetReadModel().Stages, stage => stage.Stage == "MarketOutlookComposition").Status);
    }

    [Fact]
    public void Observer_staleness_does_not_refresh_its_timestamp_or_downgrade_known_red()
    {
        var time = new ProjectionTime();
        var health = new MarketDataOperationsHealthService(new(), time);
        Observe(health, time);
        var first = health.GetReadModel();
        time.Advance(TimeSpan.FromSeconds(16));
        Assert.Equal("Orange", health.GetReadModel().OverallStatus);
        Assert.Equal(first.ObservedOnUtc, health.GetReadModel().ObservedOnUtc);
        Observe(health, time, pending: 1, age: TimeSpan.FromMinutes(2));
        time.Advance(TimeSpan.FromSeconds(16));
        Assert.Equal("Red", health.GetReadModel().OverallStatus);
    }

    [Fact]
    public void Closed_session_is_inactive_and_retains_history_and_bounded_latency_percentiles()
    {
        var time = new ProjectionTime();
        var health = new MarketDataOperationsHealthService(new(), time);
        health.Record(new(MarketDataOperationStage.MarketOutlookComposition, MarketDataOperationOutcome.Failed,
            MarketOutlookUpdateKind.EsTrade, Guid.NewGuid(), time.GetUtcNow().UtcDateTime, TimeSpan.FromMinutes(2)));
        Observe(health, time, closed: true);
        var result = health.GetReadModel();
        Assert.Equal("Inactive", result.OverallStatus);
        var stage = Assert.Single(result.Stages, stage => stage.Stage == "MarketOutlookComposition");
        Assert.Equal(1, stage.Failed);
        Assert.Equal(TimeSpan.FromMinutes(2), stage.P99Latency);
        Assert.False(stage.Required);
    }

    static void Observe(MarketDataOperationsHealthService health, ProjectionTime time,
        int pending = 0, TimeSpan age = default, bool closed = false, MarketDataBackendStatus? backend = null) => health.ObserveRuntime(
        new MarketSessionReadModel
        {
            State = closed ? FuturesMarketState.Closed : FuturesMarketState.LiveTrading,
            ActiveValueDate = closed ? null : new DateOnly(2026, 9, 4)
        },
        new DatabentoLifecycleSnapshot
        {
            State = DatabentoLifecycleState.Healthy,
            StateRevision = 1,
            ValueDate = new(2026, 9, 4),
            CorrelationId = Guid.Empty,
            NativeGeneration = Guid.Empty,
            RecoveryAttempt = 0,
            Reason = "test",
            ChangedOnUtc = time.GetUtcNow().UtcDateTime
        }, [],
        new MarketOutlookProcessorMetricsSnapshot
        {
            Updates = new Dictionary<MarketOutlookUpdateKind, MarketOutlookUpdateMetricSnapshot>(),
            IsProcessorReady = true,
            PendingCount = pending,
            OldestPendingUtc = pending > 0 ? time.GetUtcNow().UtcDateTime - age : null
        }, null, new(), backend);

    sealed class ProjectionTime : TimeProvider
    {
        DateTimeOffset now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan value) => now += value;
    }
}
