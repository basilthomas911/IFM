using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RegimeDiscovery;

/// <summary>Qualifies the immutable RD-1 configuration, snapshot, and result contracts.</summary>
public sealed class RegimeDiscoveryContractTests
{
    /// <summary>Confirms each approved horizon default is complete and valid.</summary>
    [Theory]
    [InlineData(TimeFrameType.Daily)]
    [InlineData(TimeFrameType.Weekly)]
    [InlineData(TimeFrameType.Monthly)]
    public void Default_parameter_sets_are_valid(TimeFrameType horizon)
    {
        var parameterSet = CreateParameterSet(horizon);

        new RegimeDiscoveryParameterSetValidationRules().Execute(parameterSet).Should().BeEmpty();
        parameterSet.Horizon.TimeFrames.Where(frame => frame.IsRequired).Should().NotBeEmpty();
        parameterSet.Horizon.TimeFrames.Sum(frame => frame.Weight).Should().Be(1m);
    }

    /// <summary>Confirms invalid component weights cannot qualify.</summary>
    [Fact]
    public void Parameter_set_rejects_invalid_weights()
    {
        var parameterSet = CreateParameterSet(TimeFrameType.Daily) with
        {
            Trend = new TrendRegimeConfiguration { EmaAlignmentWeight = 0.50m }
        };

        new RegimeDiscoveryParameterSetValidationRules().Execute(parameterSet)
            .Should().Contain(error => error.ErrorMessage == "Trend weights must sum to one.");
    }

    /// <summary>Confirms the snapshot request enforces exact requirements and a supported horizon.</summary>
    [Fact]
    public void Snapshot_request_validation_is_explicit()
    {
        var request = CreateSnapshotRequest();

        new RegimeDiscoveryMarketSignalSnapshotRequestValidationRules().Execute(request).Should().BeEmpty();
        new RegimeDiscoveryMarketSignalSnapshotRequestValidationRules().Execute(request with { Requirements = [] })
            .Should().NotBeEmpty();
    }

    /// <summary>Confirms all RD-1 object contracts retain sequential MessagePack keys.</summary>
    [Fact]
    public void Message_pack_keys_are_sequential()
    {
        foreach (var type in ContractTypes)
        {
            var keys = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.GetCustomAttribute<KeyAttribute>())
                .Where(attribute => attribute is not null)
                .Select(attribute => attribute!.IntKey!.Value)
                .OrderBy(value => value)
                .ToArray();

            keys.Should().Equal(Enumerable.Range(0, keys.Length), type.Name);
        }
    }

    /// <summary>Confirms parameter, snapshot, and typed result payloads round-trip byte-for-byte.</summary>
    [Fact]
    public void Contracts_round_trip_through_message_pack()
    {
        AssertRoundTrip(CreateParameterSet(TimeFrameType.Weekly));
        AssertRoundTrip(CreateSnapshot());
        AssertRoundTrip(CreateResult());
    }

    /// <summary>Confirms every stable reason code is unique and follows the approved namespace.</summary>
    [Fact]
    public void Reason_codes_are_unique_and_stable()
    {
        var codes = typeof(RegimeDiscoveryReasonCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        codes.Should().OnlyContain(code => code.StartsWith("RD.", StringComparison.Ordinal));
        codes.Should().OnlyHaveUniqueItems();
    }

    /// <summary>Confirms startup rejects absent-equivalent, non-positive, and operationally unbounded timeouts.</summary>
    [Fact]
    public void Execution_options_enforce_bounded_positive_maximum_duration()
    {
        var valid = new RegimeDiscoveryExecutionOptions();
        var belowMinimum = new RegimeDiscoveryExecutionOptions
        {
            MaximumExecutionDuration = RegimeDiscoveryExecutionOptions.MinimumExecutionDuration -
                                       TimeSpan.FromMilliseconds(1)
        };
        var aboveMaximum = new RegimeDiscoveryExecutionOptions
        {
            MaximumExecutionDuration = RegimeDiscoveryExecutionOptions.MaximumAllowedExecutionDuration +
                                       TimeSpan.FromMilliseconds(1)
        };

        valid.Invoking(options => options.Validate()).Should().NotThrow();
        belowMinimum.Invoking(options => options.Validate()).Should().Throw<ArgumentOutOfRangeException>();
        aboveMaximum.Invoking(options => options.Validate()).Should().Throw<ArgumentOutOfRangeException>();
    }

    static readonly Type[] ContractTypes =
    [
        typeof(RegimeDiscoveryTimeFrameConfiguration),
        typeof(RegimeDiscoveryHorizonConfiguration),
        typeof(TrendRegimeConfiguration),
        typeof(VolatilityRegimeConfiguration),
        typeof(MarketStructureRegimeConfiguration),
        typeof(MarketRegimeFusionConfiguration),
        typeof(RegimeDiscoveryParameterSet),
        typeof(RegimeDiscoverySignalRequirement),
        typeof(RegimeDiscoverySignalObservation),
        typeof(RegimeDiscoveryMarketSignalSnapshotRequest),
        typeof(RegimeDiscoveryMarketSignalSnapshot),
        typeof(RegimeDiscoveryMarketSignalSnapshotResult),
        typeof(RegimeDiscoveryEvidence),
        typeof(RegimeDiscoveryReason),
        typeof(TrendRegimeResult),
        typeof(VolatilityRegimeResult),
        typeof(MarketStructureRegimeResult),
        typeof(MarketRegimeFusionResult),
        typeof(RegimeDiscoveryResult)
    ];

    static RegimeDiscoveryParameterSet CreateParameterSet(TimeFrameType horizon) =>
        RegimeDiscoveryParameterSet.CreateDefault(
            Guid.Parse("0198E212-3C00-7000-8000-000000000101"),
            Guid.Parse("0198E212-3C00-7000-8000-000000000102"),
            horizon);

    static RegimeDiscoveryMarketSignalSnapshotRequest CreateSnapshotRequest() => new()
    {
        MarketSeriesIdentity = MarketSeriesIdentity.ForContract("ES-202609"),
        TargetHorizon = TimeFrameType.Daily,
        Requirements =
        [
            new RegimeDiscoverySignalRequirement
            {
                Metric = RegimeDiscoverySignalMetric.CurrentPrice,
                TimeFrame = TimeFrameType.FifteenMinutes,
                IsRequired = true,
                CalculationConfigurationId = "Price.v1",
                MaximumAgeSeconds = 2700,
                Weight = 1m
            }
        ],
        FutureClockSkewSeconds = 5,
        SupportedSchemaVersions = [1],
        ApprovedCalculationVersions = ["1"],
        CaptureAttempts = 3
    };

    static RegimeDiscoveryMarketSignalSnapshot CreateSnapshot() => new()
    {
        SnapshotId = Guid.Parse("0198E212-3C00-7000-8000-000000000103"),
        CacheRevision = 17,
        MarketSeriesIdentity = MarketSeriesIdentity.ForContract("ES-202609"),
        TargetHorizon = TimeFrameType.Daily,
        CapturedAtUtc = Utc(16, 0),
        MarketDataAsOfUtc = Utc(15, 59),
        Observations =
        [
            new RegimeDiscoverySignalObservation
            {
                Metric = RegimeDiscoverySignalMetric.CurrentPrice,
                SignalKey = new MarketAnalyticsSignalKey(
                    MarketSeriesIdentity.ForContract("ES-202609"),
                    MarketAnalyticsSignalKind.Iti,
                    TimeFrameType.FifteenMinutes,
                    "Price.v1"),
                Value = 6500m,
                MarketDataAsOfUtc = Utc(15, 59),
                CalculatedAtUtc = Utc(15, 59),
                SourceSequence = 17,
                SchemaVersion = 1,
                CalculationVersion = "1",
                IsWarm = true,
                IsValid = true,
                Availability = RegimeDiscoverySignalAvailability.Available,
                FreshnessFactor = 0.95m,
                SignalIdentity = "ES-202609.Price.15m"
            }
        ]
    };

    static RegimeDiscoveryResult CreateResult() => new()
    {
        ResultId = Guid.Parse("0198E212-3C00-7000-8000-000000000104"),
        StrategyParameterSetId = Guid.Parse("0198E212-3C00-7000-8000-000000000102"),
        StrategyParameterSetVersion = 1,
        RegimeDiscoveryParameterSetId = Guid.Parse("0198E212-3C00-7000-8000-000000000101"),
        RegimeDiscoveryParameterSetVersion = 1,
        SignalSnapshotId = Guid.Parse("0198E212-3C00-7000-8000-000000000103"),
        TriggerEventId = Guid.Parse("0198E212-3C00-7000-8000-000000000105"),
        MarketDataAsOfUtc = Utc(15, 59),
        ProducedAtUtc = Utc(16, 0),
        TargetHorizon = TimeFrameType.Daily,
        Trend = new TrendRegimeResult
        {
            IsComplete = true,
            Direction = RegimeDirection.Up,
            Strength = TrendRegimeStrength.Strong,
            Phase = TrendRegimePhase.Established,
            Score = 0.70m,
            Confidence = 0.80m,
            ConfidenceBand = RegimeConfidenceBand.VeryHigh,
            TimeFrameAgreement = 0.90m
        },
        Volatility = new VolatilityRegimeResult
        {
            IsComplete = true,
            Level = VolatilityRegimeLevel.Normal,
            Change = VolatilityRegimeChange.Stable,
            TermStructure = VxTermStructureRegime.Contango,
            Score = 0.40m,
            Confidence = 0.80m,
            ConfidenceBand = RegimeConfidenceBand.VeryHigh
        },
        MarketStructure = new MarketStructureRegimeResult
        {
            IsComplete = true,
            Classification = MarketStructureClassification.Trending,
            Direction = RegimeDirection.Up,
            Score = 0.65m,
            Confidence = 0.80m,
            ConfidenceBand = RegimeConfidenceBand.VeryHigh
        },
        Fusion = new MarketRegimeFusionResult
        {
            IsComplete = true,
            Direction = RegimeDirection.Up,
            DirectionalScore = 0.6825m,
            RiskAdjustedConviction = 0.546m,
            Confidence = 0.80m,
            ConfidenceBand = RegimeConfidenceBand.VeryHigh,
            Quality = RegimeOverallQuality.High
        },
        OverallQuality = RegimeOverallQuality.High,
        OverallConfidence = 0.80m,
        SummaryText = "Daily Up/Normal/Trending"
    };

    static void AssertRoundTrip<T>(T value)
    {
        var bytes = MessagePackSerializer.Serialize(value);
        var roundTrip = MessagePackSerializer.Deserialize<T>(bytes);
        MessagePackSerializer.Serialize(roundTrip).Should().Equal(bytes);
    }

    static DateTime Utc(int hour, int minute) =>
        new(2026, 8, 26, hour, minute, 0, DateTimeKind.Utc);
}
