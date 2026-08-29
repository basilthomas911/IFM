using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.RegimeDiscovery;

/// <summary>Qualifies RD-5 atomic snapshot capture and explicit data availability outcomes.</summary>
public sealed class RegimeDiscoveryMarketSignalSnapshotProviderTests
{
    [Fact]
    public async Task Tdi_adapter_publishes_signed_strength_as_optional_regime_evidence()
    {
        var provider = new RegimeDiscoveryMarketSignalSnapshotProvider();
        var contract = $"ES-{Guid.NewGuid():N}";
        var now = DateTime.UtcNow.AddSeconds(-1);
        RegimeDiscoverySignalCacheAdapter.Publish(new FuturesTdiSignalReadModel
        {
            ContractId = contract,
            ValueDate = DateOnly.FromDateTime(now),
            TimePeriod = TimeFrameType.OneHour,
            Timestamp = TimeOnly.FromDateTime(now),
            TDI = FuturesTrendDirectionType.DownTrending,
            TDIStrength = FuturesTrendDirectionStrengthType.Medium,
            SchemaVersion = FuturesTdiConfiguration.CurrentSchemaVersion,
            SourceSequence = 42,
            SourceEventTimestamp = now
        });

        var result = await provider.CaptureAsync(SingleMetricRequest(
            contract, RegimeDiscoverySignalMetric.Tdi,
            TimeFrameType.OneHour, isRequired: false));

        result.IsSuccess.Should().BeTrue();
        result.Snapshot!.Observations.Should().ContainSingle().Which.Should().Match<RegimeDiscoverySignalObservation>(
            value => value.Value == -0.66m && value.SourceSequence == 42 &&
                     value.Availability == RegimeDiscoverySignalAvailability.Available);
    }

    [Fact]
    public async Task Iti_vix_futures_input_is_published_as_front_vx_not_spot_vix()
    {
        var provider = new RegimeDiscoveryMarketSignalSnapshotProvider();
        var contract = $"ES-{Guid.NewGuid():N}";
        var now = DateTime.UtcNow.AddSeconds(-1);
        RegimeDiscoverySignalCacheAdapter.Publish(new FuturesItiSignalV2ReadModel
        {
            ContractId = contract,
            ValueDate = DateOnly.FromDateTime(now),
            TimeFrameStartValueDate = DateOnly.FromDateTime(now),
            TimePeriod = TimeFrameType.Daily,
            IntrinsicTime = now,
            IntrinsicPrice = 100,
            IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend
        }, 11, now, 21m);

        var frontVx = await provider.CaptureAsync(SingleMetricRequest(
            contract, RegimeDiscoverySignalMetric.VxFrontLevel,
            TimeFrameType.Daily));
        var spotVix = await provider.CaptureAsync(SingleMetricRequest(
            contract, RegimeDiscoverySignalMetric.VixLevel,
            TimeFrameType.Daily, isRequired: false));

        frontVx.IsSuccess.Should().BeTrue();
        frontVx.Snapshot!.Observations.Should().ContainSingle(value => value.Value == 21m);
        spotVix.Snapshot!.Observations.Should().ContainSingle(value =>
            value.Availability == RegimeDiscoverySignalAvailability.Missing);
    }

    /// <summary>Confirms an exact warm compatible observation produces a revision-stable snapshot.</summary>
    [Fact]
    public async Task Exact_available_observation_produces_snapshot()
    {
        var provider = new RegimeDiscoveryMarketSignalSnapshotProvider();
        var contract = $"ES-{Guid.NewGuid():N}";
        provider.Upsert(Observation(contract));

        var result = await provider.CaptureAsync(Request(contract));

        result.IsSuccess.Should().BeTrue();
        result.Snapshot.Should().NotBeNull();
        result.Snapshot!.CacheRevision.Should().Be(provider.Revision);
        result.Snapshot.Observations.Should().ContainSingle(value =>
            value.Availability == RegimeDiscoverySignalAvailability.Available && value.FreshnessFactor > 0m);
    }

    /// <summary>Confirms every explicit incompatibility is returned without a partial successful snapshot.</summary>
    [Theory]
    [InlineData(RegimeDiscoverySignalAvailability.Stale)]
    [InlineData(RegimeDiscoverySignalAvailability.NotWarm)]
    [InlineData(RegimeDiscoverySignalAvailability.Invalid)]
    [InlineData(RegimeDiscoverySignalAvailability.FutureTimestamp)]
    [InlineData(RegimeDiscoverySignalAvailability.SchemaUnsupported)]
    [InlineData(RegimeDiscoverySignalAvailability.CalculationVersionMismatch)]
    public async Task Required_incompatible_observation_fails_explicitly(
        RegimeDiscoverySignalAvailability expected)
    {
        var provider = new RegimeDiscoveryMarketSignalSnapshotProvider();
        var contract = $"ES-{Guid.NewGuid():N}";
        var source = Observation(contract);
        source = expected switch
        {
            RegimeDiscoverySignalAvailability.Stale => source with
                { MarketDataAsOfUtc = DateTime.UtcNow.AddHours(-2) },
            RegimeDiscoverySignalAvailability.NotWarm => source with { IsWarm = false },
            RegimeDiscoverySignalAvailability.Invalid => source with { IsValid = false },
            RegimeDiscoverySignalAvailability.FutureTimestamp => source with
                { MarketDataAsOfUtc = DateTime.UtcNow.AddMinutes(5) },
            RegimeDiscoverySignalAvailability.SchemaUnsupported => source with { SchemaVersion = 2 },
            RegimeDiscoverySignalAvailability.CalculationVersionMismatch => source with
                { CalculationVersion = "2" },
            _ => source
        };
        provider.Upsert(source);

        var result = await provider.CaptureAsync(Request(contract));

        result.IsSuccess.Should().BeFalse();
        result.Snapshot.Should().BeNull();
        result.Issues.Should().ContainSingle(value => value.Availability == expected);
    }

    /// <summary>Confirms a missing required metric fails while a missing optional metric remains observable.</summary>
    [Fact]
    public async Task Required_and_optional_missing_have_distinct_success_rules()
    {
        var provider = new RegimeDiscoveryMarketSignalSnapshotProvider();
        var required = await provider.CaptureAsync(Request($"ES-{Guid.NewGuid():N}"));
        var optionalRequest = Request($"ES-{Guid.NewGuid():N}") with
        {
            Requirements = [Request("unused").Requirements[0] with { IsRequired = false }]
        };
        var optional = await provider.CaptureAsync(optionalRequest);

        required.IsSuccess.Should().BeFalse();
        required.Issues.Should().ContainSingle(value =>
            value.Availability == RegimeDiscoverySignalAvailability.Missing);
        optional.IsSuccess.Should().BeTrue();
        optional.Snapshot.Should().NotBeNull();
        optional.Issues.Should().ContainSingle();
    }

    static RegimeDiscoveryMarketSignalSnapshotRequest Request(string contract) => new()
    {
        MarketSeriesIdentity = MarketSeriesIdentity.ForContract(contract),
        TargetHorizon = TimeFrameType.Daily,
        Requirements =
        [
            new RegimeDiscoverySignalRequirement
            {
                Metric = RegimeDiscoverySignalMetric.Ema20,
                TimeFrame = TimeFrameType.Daily,
                IsRequired = true,
                CalculationConfigurationId = "Ema20.v1",
                MaximumAgeSeconds = 3600,
                Weight = 1m
            }
        ],
        FutureClockSkewSeconds = 1,
        SupportedSchemaVersions = [1],
        ApprovedCalculationVersions = ["1"],
        CaptureAttempts = 3
    };

    static RegimeDiscoveryMarketSignalSnapshotRequest SingleMetricRequest(
        string contract,
        RegimeDiscoverySignalMetric metric,
        TimeFrameType timeFrame,
        bool isRequired = true) => new()
    {
        MarketSeriesIdentity = MarketSeriesIdentity.ForContract(contract),
        TargetHorizon = TimeFrameType.Daily,
        Requirements =
        [
            new RegimeDiscoverySignalRequirement
            {
                Metric = metric,
                TimeFrame = timeFrame,
                IsRequired = isRequired,
                CalculationConfigurationId = $"{metric}.v1",
                MaximumAgeSeconds = 3600,
                Weight = 1m
            }
        ],
        FutureClockSkewSeconds = 1,
        SupportedSchemaVersions = [1],
        ApprovedCalculationVersions = ["1"],
        CaptureAttempts = 3
    };

    static RegimeDiscoverySignalObservation Observation(string contract)
    {
        var key = new MarketAnalyticsSignalKey(MarketSeriesIdentity.ForContract(contract),
            MarketAnalyticsSignalKind.Ema, TimeFrameType.Daily, "Ema20.v1");
        return new RegimeDiscoverySignalObservation
        {
            Metric = RegimeDiscoverySignalMetric.Ema20,
            SignalKey = key,
            Value = 100m,
            MarketDataAsOfUtc = DateTime.UtcNow.AddSeconds(-1),
            CalculatedAtUtc = DateTime.UtcNow,
            SourceSequence = 1,
            SchemaVersion = 1,
            CalculationVersion = "1",
            IsWarm = true,
            IsValid = true,
            Availability = RegimeDiscoverySignalAvailability.Available,
            SignalIdentity = $"{contract}.Ema20.Daily"
        };
    }
}
