using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.RegimeDiscovery;

public sealed class IndicatorCacheRecoveryFeatureTests
{
    [Theory]
    [InlineData(TimeFrameType.OneHour)]
    [InlineData(TimeFrameType.FourHours)]
    public async Task GivenHistoricalSeed_WhenLiveSequenceRestarts_ThenConsumerReceivesNewerWarmObservation(TimeFrameType frame)
    {
        var provider = new RegimeDiscoveryMarketSignalSnapshotProvider();
        var series = MarketSeriesIdentity.ForContract("ES-" + Guid.NewGuid().ToString("N"));
        var now = DateTime.UtcNow;
        var observation = new RegimeDiscoverySignalObservation
        {
            Metric = RegimeDiscoverySignalMetric.Atr14, SignalKey = new(series, MarketAnalyticsSignalKind.Atr, frame, "Atr14.v1"),
            Value = 2m, MarketDataAsOfUtc = now.AddHours(-8), CalculatedAtUtc = now.AddHours(-8),
            SourceSequence = 9_000_000, SchemaVersion = 1, CalculationVersion = "1", IsWarm = true, IsValid = true,
            SignalIdentity = "atr-test", Availability = RegimeDiscoverySignalAvailability.Available
        };
        provider.Upsert(observation);
        provider.Upsert(observation with { MarketDataAsOfUtc = now.AddSeconds(-1), CalculatedAtUtc = now, SourceSequence = 2, Value = 3m });
        var result = await provider.CaptureAsync(new()
        {
            MarketSeriesIdentity = series, TargetHorizon = TimeFrameType.Daily,
            Requirements = [new() { Metric = RegimeDiscoverySignalMetric.Atr14, TimeFrame = frame, CalculationConfigurationId = "Atr14.v1", IsRequired = true, MaximumAgeSeconds = 60, Weight = 1m }],
            FutureClockSkewSeconds = 2, SupportedSchemaVersions = [1], ApprovedCalculationVersions = ["1"], CaptureAttempts = 3
        });
        Assert.True(result.IsSuccess);
        Assert.Equal(3m, Assert.Single(result.Snapshot!.Observations).Value);
    }
}
