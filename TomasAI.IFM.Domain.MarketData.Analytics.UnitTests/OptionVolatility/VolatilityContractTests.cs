using System.Collections.Immutable;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.OptionVolatility;

public sealed class VolatilityContractTests
{
    [Fact]
    public void SeriesDefinition_CarriesVersionedComparableSeriesAndGovernanceMetadata()
    {
        var identity = new VolatilitySeriesIdentity("ES-ATM-CM", "methodology-v7");
        var definition = new VolatilitySeriesDefinition(
            VolatilitySeriesDefinition.CurrentSchemaVersion,
            identity,
            "ES",
            "XCME",
            "USD",
            new("simulation", "fixture", "quotes-v2"),
            new(30, VolatilityMoneynessConvention.AtTheMoneyForward,
                VolatilityOptionSideSelection.CallPutCombined, ["EW", "EOM"],
                VolatilitySideCombinationMethod.ArithmeticMean),
            new("European", "FuturesStyle", "DeliveryOfFuture", ["black76-v3"], "solver-v2"),
            new("executable-mid", "liquidity-v4", true,
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(250)),
            new("bracketing-v2", VolatilityInterpolationMethod.LinearTotalVariance,
                "es-roll-v3", "cme-calendar-2026", "America/Chicago", "daily-close-v2",
                TimeSpan.FromMinutes(5), 12),
            Policy(),
            new(new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), null,
                "approved-config-11", "market-data", "evidence-42", ["methodology-v6"]));

        definition.Identity.Should().Be(identity);
        definition.Metrics.PolicyVersion.Should().Be("metric-v2");
        definition.Construction.ConstantTenorInterpolation
            .Should().Be(VolatilityInterpolationMethod.LinearTotalVariance);
        definition.Governance.CompatibleHistoricalMethodologyVersions
            .Should().Equal("methodology-v6");
    }

    [Theory]
    [InlineData(VolatilityDependencyRequirement.Required)]
    [InlineData(VolatilityDependencyRequirement.Optional)]
    public void WorkflowDependencyPolicy_ExpressesRequiredAndOptionalSemantics(
        VolatilityDependencyRequirement requirement)
    {
        var policy = new VolatilityWorkflowDependencyPolicy(
            VolatilityWorkflowDependencyPolicy.CurrentSchemaVersion,
            "trade-selection-volatility",
            "dependency-v3",
            new("ES-ATM-CM", "methodology-v7"),
            "metric-v2",
            requirement);

        policy.Requirement.Should().Be(requirement);
        policy.DependencyPolicyVersion.Should().Be("dependency-v3");
    }

    [Fact]
    public void MetricSnapshot_PreservesIndependentStatusesUnitsTimingAndSourceManifest()
    {
        var snapshot = new OptionIvMetricSnapshot(
            OptionIvMetricSnapshot.CurrentSchemaVersion,
            "snapshot-1",
            "snapshot-digest",
            new("ES-ATM-CM", "methodology-v7"),
            "metric-v2",
            new(2026, 9, 18),
            "daily-close",
            0.20m,
            VolatilityValueUnit.AnnualDecimal,
            null,
            VolatilityMetricStatus.UndefinedRange,
            0m,
            VolatilityMetricStatus.Qualified,
            VolatilityMetricUnit.PercentagePoints0To100,
            0.20m,
            0.20m,
            0,
            4,
            4,
            4,
            1m,
            new(2026, 9, 14),
            new(2026, 9, 17),
            ["observation-1", "observation-2"],
            "source-digest",
            "calculator-v1",
            new(2026, 9, 18, 21, 0, 0, TimeSpan.Zero),
            new(2026, 9, 18, 21, 0, 1, TimeSpan.Zero),
            new(2026, 9, 18, 21, 0, 2, TimeSpan.Zero),
            new(2026, 9, 18, 21, 0, 3, TimeSpan.Zero));

        snapshot.RankStatus.Should().Be(VolatilityMetricStatus.UndefinedRange);
        snapshot.PercentileStatus.Should().Be(VolatilityMetricStatus.Qualified);
        snapshot.ImpliedVolatilityUnit.Should().Be(VolatilityValueUnit.AnnualDecimal);
        snapshot.MetricUnit.Should().Be(VolatilityMetricUnit.PercentagePoints0To100);
        snapshot.AvailableAtUtc.Should().BeAfter(snapshot.RecordedAtUtc);
        snapshot.SourceObservationIds.Should().HaveCount(2);
    }

    [Fact]
    public void WorkflowEvidence_ReferencesExactAcceptedSnapshotAndPolicyVersions()
    {
        var evidence = new VolatilityWorkflowEvidence(
            VolatilityWorkflowEvidence.CurrentSchemaVersion,
            "snapshot-1",
            "sha256",
            new("ES-ATM-CM", "methodology-v7"),
            "metric-v2",
            "trade-selection-volatility",
            "dependency-v3",
            VolatilityDependencyRequirement.Required,
            new(2026, 9, 18, 21, 1, 0, TimeSpan.Zero),
            VolatilityFreshnessStatus.Accepted,
            "VOLATILITY.ACCEPTED");

        evidence.SnapshotId.Should().Be("snapshot-1");
        evidence.DependencyPolicyVersion.Should().Be("dependency-v3");
        evidence.FreshnessStatus.Should().Be(VolatilityFreshnessStatus.Accepted);
    }

    static VolatilityMetricPolicy Policy() =>
        new(
            "metric-v2",
            252,
            240,
            0.95m,
            VolatilityGapPolicy.PreserveExpectedSessionGap,
            VolatilityHistoricalWindowConvention.PriorExchangeSessions,
            VolatilityRankRangeConvention.CurrentAndPriorWindow,
            VolatilityPercentileTieConvention.StrictlyLessThanCurrent);
}
