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
    /// <summary>Historical replay and live workers have independent sequence domains; newer market evidence must advance the cache.</summary>
    [Fact]
    public async Task Newer_market_observation_replaces_historical_seed_with_larger_sequence()
    {
        var provider = new RegimeDiscoveryMarketSignalSnapshotProvider();
        var contract = $"ES-{Guid.NewGuid():N}";
        var historical = Observation(contract) with { MarketDataAsOfUtc = DateTime.UtcNow.AddHours(-2), SourceSequence = 9_000_000 };
        var live = historical with { MarketDataAsOfUtc = DateTime.UtcNow.AddSeconds(-1), SourceSequence = 5, Value = 123m };
        provider.Upsert(historical);
        provider.Upsert(live);

        var result = await provider.CaptureAsync(Request(contract));

        result.IsSuccess.Should().BeTrue();
        result.Snapshot!.Observations.Single().Value.Should().Be(123m);
        result.Snapshot.Observations.Single().SourceSequence.Should().Be(5);
    }

    /// <summary>A delayed restore must not replace a newer market observation merely because its sequence is larger.</summary>
    [Fact]
    public async Task Older_restore_cannot_replace_newer_live_evidence()
    {
        var provider = new RegimeDiscoveryMarketSignalSnapshotProvider();
        var contract = $"ES-{Guid.NewGuid():N}";
        var live = Observation(contract) with { SourceSequence = 5, Value = 123m };
        provider.Upsert(live);
        provider.Upsert(live with { MarketDataAsOfUtc = live.MarketDataAsOfUtc.AddHours(-1), SourceSequence = 9_000_000, Value = 99m });

        var result = await provider.CaptureAsync(Request(contract));

        result.Snapshot!.Observations.Single().Value.Should().Be(123m);
    }

    /// <summary>Duplicates do not change the revision, and equal-timestamp corrections retain source-sequence ordering.</summary>
    [Fact]
    public async Task Duplicate_does_not_change_revision_but_equal_timestamp_correction_does()
    {
        var provider = new RegimeDiscoveryMarketSignalSnapshotProvider();
        var contract = $"ES-{Guid.NewGuid():N}";
        var observation = Observation(contract);
        provider.Upsert(observation);
        var before = provider.Revision;
        provider.Upsert(observation);
        provider.Revision.Should().Be(before);
        provider.Upsert(observation with { SourceSequence = 2, Value = 101m });

        var result = await provider.CaptureAsync(Request(contract));

        result.Snapshot!.Observations.Single().Value.Should().Be(101m);
    }

    [Fact]
    public async Task Structure_restore_uses_daily_ema_and_keeps_newer_live_intervals()
    {
        var contract = $"ES-{Guid.NewGuid():N}";
        var end = DateTimeOffset.UtcNow.AddMinutes(-1);
        RegimeDiscoverySignalCacheAdapter.Publish(new FuturesAtrSignalReadModel
        {
            ContractId = contract, TimePeriod = TimeFrameType.OneHour, AtrValue = 2,
            ValueDate = DateOnly.FromDateTime(end.UtcDateTime), Timestamp = TimeOnly.FromDateTime(end.UtcDateTime), IsWarm = true
        });
        RegimeDiscoverySignalCacheAdapter.Publish(new TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal.FuturesEmaSignalReadModel
        {
            Metadata = new()
            {
                ContractId = contract, SignalKey = new(MarketSeriesIdentity.ForContract(contract), MarketAnalyticsSignalKind.Ema, TimeFrameType.Daily, "ema"),
                ValueDate = DateOnly.FromDateTime(end.UtcDateTime), MarketDataAsOfUtc = end, CalculatedAtUtc = end,
                SchemaVersion = 1, CalculationVersion = "1", IsValid = true
            }, Ema20 = 100, IsWarm = true
        });
        var bars = Enumerable.Range(0, 21).Select(i => new TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal.FuturesTradeSessionBarReadModel
        {
            ContractId = contract, TimeFrame = TimeFrameType.OneHour, ValueDate = DateOnly.FromDateTime(end.UtcDateTime),
            IntervalEndUtc = end.AddHours(i - 20), LastMarketEventUtc = end.AddHours(i - 20), CalculatedAtUtc = end,
            High = 110, Low = 90, Close = 104, LastSourceSequence = i + 1
        }).ToArray();
        RegimeDiscoverySignalCacheAdapter.RestoreBars(bars);
        var live = bars[^1] with { IntervalEndUtc = end.AddSeconds(1), LastMarketEventUtc = end.AddSeconds(1), Close = 106, LastSourceSequence = 22 };
        RegimeDiscoverySignalCacheAdapter.Publish(live);
        RegimeDiscoverySignalCacheAdapter.RestoreBars(bars);
        RegimeDiscoverySignalCacheAdapter.Publish(bars[0]);
        var snapshot = await new RegimeDiscoveryMarketSignalSnapshotProvider().CaptureAsync(
            SingleMetricRequest(contract, RegimeDiscoverySignalMetric.Ema20Interaction, TimeFrameType.OneHour));
        snapshot.IsSuccess.Should().BeTrue();
        snapshot.Snapshot!.Observations.Single().Value.Should().Be(3m);
        snapshot.Snapshot.Observations.Single().SourceSequence.Should().Be(22);
    }

    [Fact]
    public async Task Continuous_series_evidence_is_available_to_its_exact_contract_without_losing_provenance()
    {
        var contract = $"ES-{Guid.NewGuid():N}";
        var series = MarketSeriesIdentity.ForFuturesSeries(new("ES", "calendar-front", "unadjusted", 1));
        var now = DateTimeOffset.UtcNow.AddSeconds(-1);
        var metadata = new MarketAnalyticsSignalMetadata
        {
            ContractId = contract,
            SignalKey = new(series, MarketAnalyticsSignalKind.Ema, TimeFrameType.FiveMinutes, "ema-10-20-50-200-v1"),
            ValueDate = DateOnly.FromDateTime(now.UtcDateTime),
            MarketDataAsOfUtc = now, CalculatedAtUtc = now,
            SourceSequence = 987654321, SchemaVersion = 1, CalculationVersion = "1", IsValid = true
        };
        RegimeDiscoverySignalCacheAdapter.Publish(new TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal.FuturesEmaSignalReadModel
        { Metadata = metadata, Ema20 = 123m, IsWarm = true });
        var provider = new RegimeDiscoveryMarketSignalSnapshotProvider();
        var request = SingleMetricRequest(contract, RegimeDiscoverySignalMetric.Ema20, TimeFrameType.FiveMinutes);
        foreach (var identity in new[] { MarketSeriesIdentity.ForContract(contract), series })
        {
            var result = await provider.CaptureAsync(request with { MarketSeriesIdentity = identity });
            result.IsSuccess.Should().BeTrue();
            var observation = result.Snapshot!.Observations.Single();
            observation.Value.Should().Be(123m);
            observation.SourceSequence.Should().Be(metadata.SourceSequence);
            observation.MarketDataAsOfUtc.Should().Be(now.UtcDateTime);
        }
        (await provider.CaptureAsync(request with { MarketSeriesIdentity = MarketSeriesIdentity.ForContract(contract + "-OTHER") }))
            .IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Rsi13_cannot_satisfy_an_rsi14_requirement()
    {
        var contract = $"ES-{Guid.NewGuid():N}"; var now = DateTime.UtcNow.AddSeconds(-1);
        var signal = new FuturesRsiSignalReadModel
        {
            ContractId = contract,
            ValueDate = DateOnly.FromDateTime(now),
            TimePeriod = TimeFrameType.OneHour,
            Timestamp = TimeOnly.FromDateTime(now),
            PeriodLength = 13,
            RSI = 60,
            RSISlope = 1,
            IsWarm = true
        };
        var provider = new RegimeDiscoveryMarketSignalSnapshotProvider();
        RegimeDiscoverySignalCacheAdapter.Publish(signal);
        var missing = await provider.CaptureAsync(SingleMetricRequest(contract, RegimeDiscoverySignalMetric.Rsi14, TimeFrameType.OneHour));
        missing.IsSuccess.Should().BeFalse();
        RegimeDiscoverySignalCacheAdapter.Publish(signal with { PeriodLength = 14 });
        var available = await provider.CaptureAsync(SingleMetricRequest(contract, RegimeDiscoverySignalMetric.Rsi14, TimeFrameType.OneHour));
        available.IsSuccess.Should().BeTrue();
    }
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
    public async Task Qualified_term_structure_supplies_front_vx_without_spot_vix_or_an_iti_price()
    {
        var provider = new RegimeDiscoveryMarketSignalSnapshotProvider();
        var contract = $"ES-{Guid.NewGuid():N}";
        var now = DateTime.UtcNow.AddSeconds(-1);
        var vxContract = $"VX-{Guid.NewGuid():N}";
        RegimeDiscoverySignalCacheAdapter.Publish(new TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal.FuturesVxTermStructureSignalReadModel
        {
            FrontVxContractId = vxContract,
            ValueDate = DateOnly.FromDateTime(now),
            FrontSourceTimestampUtc = now,
            CalculatedAtUtc = now,
            FrontSourceSequence = 11,
            FrontVxPrice = 21m,
            FrontBackRatio = 0.95m,
            IsWarm = true,
            IsValid = true
        });

        var frontVx = await provider.CaptureAsync(SingleMetricRequest(
            vxContract, RegimeDiscoverySignalMetric.VxFrontLevel,
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

    [Fact]
    public async Task External_vx_ratio_is_resolved_for_the_active_es_contract()
    {
        var provider = new RegimeDiscoveryMarketSignalSnapshotProvider();
        var esContract = $"ES-{Guid.NewGuid():N}";
        var vxContract = $"VX-{Guid.NewGuid():N}";
        var key = new MarketAnalyticsSignalKey(MarketSeriesIdentity.ForContract(vxContract),
            MarketAnalyticsSignalKind.VxTermStructure, TimeFrameType.Daily,
            "VxFrontSecondRatio.v1");
        provider.Upsert(Observation(vxContract) with
        {
            Metric = RegimeDiscoverySignalMetric.VxFrontSecondRatio,
            SignalKey = key,
            Value = 0.95m
        });

        var result = await provider.CaptureAsync(SingleMetricRequest(
            esContract, RegimeDiscoverySignalMetric.VxFrontSecondRatio, TimeFrameType.Daily));

        result.IsSuccess.Should().BeTrue();
        result.Snapshot!.Observations.Should().ContainSingle().Which.Value.Should().Be(0.95m);
    }

    [Fact]
    public async Task Missing_external_vx_ratio_returns_an_explicit_issue()
    {
        var provider = new RegimeDiscoveryMarketSignalSnapshotProvider();
        var result = await provider.CaptureAsync(SingleMetricRequest(
            $"ES-{Guid.NewGuid():N}", RegimeDiscoverySignalMetric.VxFrontSecondRatio,
            TimeFrameType.Monthly));

        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle().Which.Availability.Should()
            .Be(RegimeDiscoverySignalAvailability.Missing);
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
