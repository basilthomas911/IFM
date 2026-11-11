using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.RegimeDiscovery;

public sealed class RegimeDiscoverySessionFreshnessTests
{
    [Theory]
    [InlineData(RegimeDiscoverySignalMetric.Rsi14, MarketAnalyticsSignalKind.Rsi)]
    [InlineData(RegimeDiscoverySignalMetric.Rsi14Slope, MarketAnalyticsSignalKind.Rsi)]
    [InlineData(RegimeDiscoverySignalMetric.Adx14, MarketAnalyticsSignalKind.Adx)]
    [InlineData(RegimeDiscoverySignalMetric.PlusDi14, MarketAnalyticsSignalKind.Adx)]
    [InlineData(RegimeDiscoverySignalMetric.MinusDi14, MarketAnalyticsSignalKind.Adx)]
    [InlineData(RegimeDiscoverySignalMetric.MacdHistogram, MarketAnalyticsSignalKind.Macd)]
    [InlineData(RegimeDiscoverySignalMetric.Atr14, MarketAnalyticsSignalKind.Atr)]
    public async Task Last_close_evidence_qualifies_after_reopening_without_rewriting_source_time(
        RegimeDiscoverySignalMetric metric, MarketAnalyticsSignalKind kind)
    {
        var source = DateTime.Parse("2026-11-10T21:59:00Z").ToUniversalTime();
        var captured = DateTimeOffset.Parse("2026-11-10T23:02:00Z");
        var provider = new RegimeDiscoveryMarketSignalSnapshotProvider(new FixedClock(captured));
        var series = MarketSeriesIdentity.ForContract("ES-" + Guid.NewGuid().ToString("N"));
        provider.Upsert(new() { SignalKey = new(series, kind, TimeFrameType.FifteenMinutes, metric + ".v1"),
            Metric = metric, Value = 10m, IsWarm = true, IsValid = true, Availability = RegimeDiscoverySignalAvailability.Available,
            MarketDataAsOfUtc = source, CalculatedAtUtc = source, SourceSequence = 5, SchemaVersion = 1, CalculationVersion = "1" });
        var result = await provider.CaptureAsync(new() { MarketSeriesIdentity = series, TargetHorizon = TimeFrameType.Daily,
            Requirements = [new() { Metric = metric, TimeFrame = TimeFrameType.FifteenMinutes, CalculationConfigurationId = metric + ".v1",
                MaximumAgeSeconds = 900, IsRequired = true, Weight = 1m }], FutureClockSkewSeconds = 2,
            SupportedSchemaVersions = [1], ApprovedCalculationVersions = ["1"], CaptureAttempts = 3 });
        Assert.True(result.IsSuccess);
        var observation = Assert.Single(result.Snapshot!.Observations);
        Assert.Equal(source, observation.MarketDataAsOfUtc);
        Assert.Equal(0.8m, observation.FreshnessFactor);
    }

    [Theory]
    [InlineData("2026-11-06T21:59:00Z", "2026-11-08T23:02:00Z")]
    [InlineData("2026-10-30T20:59:00Z", "2026-11-01T23:02:00Z")]
    public void Weekend_and_dst_closure_preserve_exact_open_market_age(string source, string captured)
        => Assert.Equal(TimeSpan.FromMinutes(3), Age(source, captured, new()));

    [Fact]
    public void Holidays_and_early_closes_use_the_supplied_calendar()
    {
        Assert.Equal(TimeSpan.FromMinutes(3), Age("2026-11-10T21:59:00Z", "2026-11-11T23:02:00Z",
            new([new DateOnly(2026, 11, 11)])));
        Assert.Equal(TimeSpan.FromMinutes(3), Age("2026-11-10T17:59:00Z", "2026-11-10T23:02:00Z",
            new(earlyCloses: new Dictionary<DateOnly, TimeOnly> { [new(2026, 11, 10)] = new(13, 0) })));
    }

    [Theory]
    [InlineData("2026-11-10T21:40:00Z", "2026-11-10T23:02:00Z")]
    [InlineData("2026-11-10T23:00:00Z", "2026-11-10T23:15:01Z")]
    public void Missing_open_market_bars_still_exceed_the_unchanged_age_limit(string source, string captured)
        => Assert.True(Age(source, captured, new()) > TimeSpan.FromMinutes(15));

    [Theory]
    [InlineData(TimeFrameType.Daily, RegimeDiscoverySignalMetric.Ema20)]
    [InlineData(TimeFrameType.FifteenMinutes, RegimeDiscoverySignalMetric.CurrentPrice)]
    [InlineData(TimeFrameType.FifteenMinutes, RegimeDiscoverySignalMetric.ItiDirection)]
    public async Task Daily_baselines_and_realtime_evidence_also_exclude_closed_sessions(TimeFrameType frame, RegimeDiscoverySignalMetric metric)
    {
        var source = DateTimeOffset.Parse("2026-11-10T21:59:00Z").UtcDateTime;
        var captured = DateTimeOffset.Parse("2026-11-10T23:02:00Z").UtcDateTime;
        var provider = new RegimeDiscoveryMarketSignalSnapshotProvider(new FixedClock(new(captured)));
        var series = MarketSeriesIdentity.ForContract("ES-" + Guid.NewGuid().ToString("N"));
        var kind = metric == RegimeDiscoverySignalMetric.Ema20 ? MarketAnalyticsSignalKind.Ema : MarketAnalyticsSignalKind.Iti;
        provider.Upsert(new() { SignalKey = new(series, kind, frame, "session.v1"), Metric = metric,
            Value = 10m, IsWarm = true, IsValid = true, Availability = RegimeDiscoverySignalAvailability.Available,
            MarketDataAsOfUtc = source, CalculatedAtUtc = source, SourceSequence = 5, SchemaVersion = 1, CalculationVersion = "1" });
        var result = await provider.CaptureAsync(new() { MarketSeriesIdentity = series, TargetHorizon = TimeFrameType.Daily,
            Requirements = [new() { Metric = metric, TimeFrame = frame, CalculationConfigurationId = "session.v1",
                MaximumAgeSeconds = 900, IsRequired = true, Weight = 1m }], FutureClockSkewSeconds = 2,
            SupportedSchemaVersions = [1], ApprovedCalculationVersions = ["1"], CaptureAttempts = 3 });
        Assert.True(result.IsSuccess);
        Assert.Equal(0.8m, Assert.Single(result.Snapshot!.Observations).FreshnessFactor);
    }

    static TimeSpan Age(string source, string captured, CmeFuturesMarketSessionCalendar calendar)
        => MarketTradingTime.Elapsed(calendar, DateTimeOffset.Parse(source), DateTimeOffset.Parse(captured), TimeSpan.FromMinutes(15));

    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
