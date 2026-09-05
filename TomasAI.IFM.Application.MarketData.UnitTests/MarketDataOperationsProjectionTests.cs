using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class MarketDataOperationsProjectionTests
{
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
        int pending = 0, TimeSpan age = default, bool closed = false) => health.ObserveRuntime(
        new MarketSessionReadModel { State = closed ? FuturesMarketState.Closed : FuturesMarketState.LiveTrading,
            ActiveValueDate = closed ? null : new DateOnly(2026, 9, 4) },
        new DatabentoLifecycleSnapshot { State = DatabentoLifecycleState.Healthy, StateRevision = 1,
            ValueDate = new(2026, 9, 4), CorrelationId = Guid.Empty, NativeGeneration = Guid.Empty,
            RecoveryAttempt = 0, Reason = "test", ChangedOnUtc = time.GetUtcNow().UtcDateTime }, [],
        new MarketOutlookProcessorMetricsSnapshot { Updates = new Dictionary<MarketOutlookUpdateKind, MarketOutlookUpdateMetricSnapshot>(),
            IsProcessorReady = true, PendingCount = pending,
            OldestPendingUtc = pending > 0 ? time.GetUtcNow().UtcDateTime - age : null }, null, new());

    sealed class ProjectionTime : TimeProvider
    {
        DateTimeOffset now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan value) => now += value;
    }
}
