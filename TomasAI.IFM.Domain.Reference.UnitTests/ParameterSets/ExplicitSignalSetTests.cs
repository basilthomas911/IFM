using System.Text.Json;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

public sealed class ExplicitSignalSetTests
{
    [Theory]
    [InlineData(TimeFrameType.Daily)]
    [InlineData(TimeFrameType.Weekly)]
    [InlineData(TimeFrameType.Monthly)]
    public void Every_horizon_has_only_required_intervals_and_valid_explicit_membership(TimeFrameType horizon)
    {
        var seed = RegimeDiscoveryParameterModel.CreateExplicitSeed(Guid.NewGuid(), horizon);
        seed.SchemaVersion.Should().Be(ParameterSchemaRegistry.CurrentRegimeSchemaVersion);
        seed.TargetHorizon.Should().Be(horizon);
        seed.Horizon.TimeFrames.Should().OnlyContain(frame => frame.IsRequired);
        seed.SignalRequirements.Should().OnlyContain(row => row.IsRequired);
        new RegimeDiscoveryParameterModel().Validate(JsonSerializer.Serialize(seed), ParameterSchemaRegistry.CurrentRegimeSchemaVersion).Should().BeEmpty();
        seed.SignalRequirements.Should().Contain(row => row.Enabled && row.Metric == RegimeDiscoverySignalMetric.VxFrontSecondRatio);
        var expected = horizon switch
        {
            TimeFrameType.Daily => new[]{TimeFrameType.FifteenSeconds,TimeFrameType.OneMinute,TimeFrameType.FiveMinutes},
            TimeFrameType.Weekly => new[]{TimeFrameType.OneHour,TimeFrameType.FourHours,TimeFrameType.FifteenMinutes,TimeFrameType.Daily},
            _ => new[]{TimeFrameType.FourHours,TimeFrameType.Daily,TimeFrameType.OneHour}
        };
        seed.Horizon.TimeFrames.Select(frame => frame.TimeFrame).Should().BeEquivalentTo(expected);
    }
    [Theory]
    [InlineData(TimeFrameType.Daily, TimeFrameType.FiveMinutes)]
    [InlineData(TimeFrameType.Weekly, TimeFrameType.Daily)]
    [InlineData(TimeFrameType.Monthly, TimeFrameType.Daily)]
    public void Atr_baseline_ratio_is_projected_only_at_the_target_evidence_timeframe(
        TimeFrameType horizon, TimeFrameType targetEvidenceTimeFrame)
    {
        var seed = RegimeDiscoveryParameterModel.CreateExplicitSeed(Guid.NewGuid(), horizon);

        seed.SignalRequirements!.Where(row => row.Metric == RegimeDiscoverySignalMetric.Atr14)
            .Select(row => row.TimeFrame)
            .Should().BeEquivalentTo(seed.Horizon.TimeFrames.Select(frame => frame.TimeFrame));
        seed.SignalRequirements.Where(row => row.Metric == RegimeDiscoverySignalMetric.AtrBaselineRatio)
            .Select(row => row.TimeFrame)
            .Should().Equal(targetEvidenceTimeFrame);
    }

    [Fact]
    public void An_included_signal_cannot_be_optional_even_if_the_calculation_can_omit_it()
    {
        var seed = RegimeDiscoveryParameterModel.CreateExplicitSeed(Guid.NewGuid());
        seed = seed with { SignalRequirements = seed.SignalRequirements!.Select(row =>
            row.Metric == RegimeDiscoverySignalMetric.Tdi ? row with { Enabled = true, IsRequired = false } : row).ToArray() };
        new RegimeDiscoveryParameterModel().Validate(JsonSerializer.Serialize(seed), ParameterSchemaRegistry.CurrentRegimeSchemaVersion).Should().NotBeEmpty();
    }
    [Fact]
    public void Removing_a_calculation_dependency_is_rejected()
    {
        var seed = RegimeDiscoveryParameterModel.CreateExplicitSeed(Guid.NewGuid());
        seed = seed with { SignalRequirements = seed.SignalRequirements!.Select(row =>
            row.Metric == RegimeDiscoverySignalMetric.Ema200 ? row with { Enabled = false } : row).ToArray() };
        new RegimeDiscoveryParameterModel().Validate(JsonSerializer.Serialize(seed), ParameterSchemaRegistry.CurrentRegimeSchemaVersion)
            .Should().Contain(issue => issue.Code == "SIGNAL.DEPENDENCY_MISSING");
    }
    [Fact]
    public void Former_optional_intervals_cannot_be_ignored_in_schema_three()
    {
        var seed = RegimeDiscoveryParameterModel.CreateExplicitSeed(Guid.NewGuid(), TimeFrameType.Weekly);
        seed = seed with { Horizon = seed.Horizon with { TimeFrames = seed.Horizon.TimeFrames
            .Select(frame => frame.TimeFrame == TimeFrameType.Daily ? frame with { IsRequired = false } : frame).ToArray() } };
        new RegimeDiscoveryParameterModel().Validate(JsonSerializer.Serialize(seed), ParameterSchemaRegistry.CurrentRegimeSchemaVersion).Should().NotBeEmpty();
    }
    [Fact]
    public void Legacy_upgrade_regenerates_dependencies_without_carrying_vix_into_futures()
    {
        var old=RegimeDiscoveryParameterModel.CreateSeed(Guid.NewGuid());
        var before=JsonSerializer.Serialize(old);
        var upgraded=RegimeDiscoveryParameterModel.UpgradeToExplicitSet(old);
        new RegimeDiscoveryParameterModel().Validate(JsonSerializer.Serialize(upgraded),ParameterSchemaRegistry.CurrentRegimeSchemaVersion)
            .Should().BeEmpty();
        upgraded.SignalRequirements.Should().NotContain(x=>x.Metric==RegimeDiscoverySignalMetric.VixLevel);
        upgraded.ObservationMetrics.Should().HaveCount(3).And.ContainSingle(x=>x.Metric==
            TomasAI.IFM.Domain.MarketData.Shared.ObservationMetricsType.VxTermStructure&&x.Enabled);
        upgraded.ObservationMetrics.Should().ContainSingle(x=>x.Metric==
            TomasAI.IFM.Domain.MarketData.Shared.ObservationMetricsType.Vwap&&!x.Enabled);
        upgraded.ObservationMetrics.Should().ContainSingle(x=>x.Metric==
            TomasAI.IFM.Domain.MarketData.Shared.ObservationMetricsType.Iti&&!x.Enabled);
        upgraded.SignalRequirements.Should().Contain(x=>x.Enabled&&x.Metric==RegimeDiscoverySignalMetric.RollingHigh20&&x.TimeFrame==TimeFrameType.FourHours);
        JsonSerializer.Serialize(old).Should().Be(before);
    }
    [Fact]
    public void Existing_schema_five_working_copy_gains_missing_observations_without_mutating_the_saved_source()
    {
        var source=RegimeDiscoveryParameterModel.CreateExplicitSeed(Guid.NewGuid());
        source=source with {ObservationMetrics=source.ObservationMetrics!.Where(row=>row.Metric==
            TomasAI.IFM.Domain.MarketData.Shared.ObservationMetricsType.VxTermStructure).ToArray()};
        var before=JsonSerializer.Serialize(source);
        var upgraded=RegimeDiscoveryParameterModel.UpgradeToExplicitSet(source);
        upgraded.ObservationMetrics.Should().HaveCount(3);
        JsonSerializer.Serialize(source).Should().Be(before);
    }
    [Fact]
    public void An_incomplete_schema_three_draft_can_be_reopened_for_correction()
    {
        var draft=RegimeDiscoveryParameterModel.CreateExplicitSeed(Guid.NewGuid());
        draft=draft with {SignalRequirements=draft.SignalRequirements!.Select(x=>x with {Enabled=false}).ToArray()};
        RegimeDiscoveryParameterModel.UpgradeToExplicitSet(draft).Should().BeSameAs(draft);
        new RegimeDiscoveryParameterModel().Validate(JsonSerializer.Serialize(draft),ParameterSchemaRegistry.CurrentRegimeSchemaVersion).Should().NotBeEmpty();
    }
    [Fact]
    public void Removing_an_interval_rebuilds_the_set_without_waiting_for_that_interval()
    {
        var seed=RegimeDiscoveryParameterModel.CreateExplicitSeed(Guid.NewGuid());
        var edited=seed with {Horizon=seed.Horizon with {TimeFrames=seed.Horizon.TimeFrames.Where(x=>x.TimeFrame!=TimeFrameType.FiveMinutes).ToArray()}};
        var result=RegimeDiscoveryParameterModel.UpgradeToExplicitSet(edited,true);
        result.SignalRequirements.Should().NotContain(x=>x.TimeFrame==TimeFrameType.FiveMinutes);
        result.SignalRequirements.Should().Contain(x=>x.Metric==RegimeDiscoverySignalMetric.RollingHigh20&&x.TimeFrame==TimeFrameType.OneMinute&&x.Enabled);
        new RegimeDiscoveryParameterModel().Validate(JsonSerializer.Serialize(result),ParameterSchemaRegistry.CurrentRegimeSchemaVersion).Should().BeEmpty();
    }

    [Fact]
    public void Schema_three_migration_creates_a_schema_four_copy_without_mutating_the_source()
    {
        var current=RegimeDiscoveryParameterModel.CreateExplicitSeed(Guid.NewGuid());
        var source=current with
        {
            SchemaVersion=3,
            SignalRequirements=current.SignalRequirements!.Select(row=>row with {MaximumAgeSeconds=row.MaximumAgeSeconds+7}).ToArray()
        };
        var before=JsonSerializer.Serialize(source);
        var migrated=RegimeDiscoveryParameterModel.UpgradeToExplicitSet(source);
        migrated.SchemaVersion.Should().Be(ParameterSchemaRegistry.CurrentRegimeSchemaVersion);
        migrated.ParameterSetId.Should().Be(source.ParameterSetId);
        migrated.TargetHorizon.Should().Be(source.TargetHorizon);
        migrated.SignalRequirements!.Select(row=>(row.RequirementId,row.MaximumAgeSeconds))
            .Should().Equal(source.SignalRequirements.Select(row=>(row.RequirementId,row.MaximumAgeSeconds)));
        JsonSerializer.Serialize(source).Should().Be(before);
        new RegimeDiscoveryParameterModel().Validate(JsonSerializer.Serialize(migrated),ParameterSchemaRegistry.CurrentRegimeSchemaVersion)
            .Should().BeEmpty();
    }
}

