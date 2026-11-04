using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

/// <summary>Qualifies required Regime Discovery cache evidence after restart without another EMA-generated event.</summary>
public sealed class StoredBollingerRestoreTests
{
    [Theory]
    [InlineData(TimeFrameType.FifteenMinutes)]
    [InlineData(TimeFrameType.OneHour)]
    public async Task Committed_warm_bands_restore_all_three_required_metrics_without_new_market_data(TimeFrameType frame)
    {
        var databases = Substitute.For<IDbContextFactory>();
        var contract = "ES-RESTORE-" + Guid.NewGuid().ToString("N");
        var series = MarketSeriesIdentity.ForContract(contract);
        var at = DateTimeOffset.UtcNow.AddMinutes(-1);
        var date = DateOnly.FromDateTime(at.UtcDateTime);
        var stored = new FuturesBbSignalReadModel
        {
            Metadata = new() { ContractId = contract, SignalKey = new(series, MarketAnalyticsSignalKind.BollingerBand, frame, "bb"),
                ValueDate = date, MarketDataAsOfUtc = at, CalculatedAtUtc = at, SourceSequence = 42,
                IsValid = true, SchemaVersion = 1, CalculationVersion = "1" },
            IsWarm = true, Width20 = 80, Width20Ratio = 1.25m, Position20 = 0.6m
        };
        databases.MarketDataDb.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(series, date, frame, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<FuturesBbSignalReadModel?>(stored));
        Assert.True(await StoredSessionIndicatorWarmup.RestoreBollingerAsync(databases, contract, date, frame, default));
        RegimeDiscoverySignalMetric[] metrics = [RegimeDiscoverySignalMetric.BollingerWidth, RegimeDiscoverySignalMetric.BollingerWidthRatio, RegimeDiscoverySignalMetric.BollingerPosition];
        var snapshot = await new RegimeDiscoveryMarketSignalSnapshotProvider().CaptureAsync(new()
        {
            MarketSeriesIdentity = series, TargetHorizon = TimeFrameType.Daily, SupportedSchemaVersions = [1], ApprovedCalculationVersions = ["1"],
            Requirements = metrics.Select(metric => new RegimeDiscoverySignalRequirement
            { Metric = metric, TimeFrame = frame, IsRequired = true, CalculationConfigurationId = $"{metric}.v1", MaximumAgeSeconds = 3600, Weight = 1 }).ToArray()
        });
        Assert.True(snapshot.IsSuccess);
        Assert.Equal(3, snapshot.Snapshot!.Observations.Length);
        Assert.All(snapshot.Snapshot.Observations, observation => Assert.Equal(42, observation.SourceSequence));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_cold_bands_keep_initialization_retryable(bool cold)
    {
        var databases = Substitute.For<IDbContextFactory>();
        var contract = "ES-COLD-" + Guid.NewGuid().ToString("N");
        var date = new DateOnly(2026, 11, 4);
        databases.MarketDataDb.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(MarketSeriesIdentity.ForContract(contract), date, TimeFrameType.OneHour, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<FuturesBbSignalReadModel?>(cold ? new() { IsWarm = false } : null));
        Assert.False(await StoredSessionIndicatorWarmup.RestoreBollingerAsync(databases, contract, date, TimeFrameType.OneHour, default));
    }
}
