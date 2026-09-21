using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;

public sealed class VolatilityEvidenceConsumerTests
{
    [Fact]
    public async Task Required_missing_stale_or_unqualified_evidence_blocks_new_entry_with_explicit_reason()
    {
        var baseline = await TradeSelectionFixture.Command();

        Evaluate(WithVolatility(baseline, VolatilityDependencyRequirement.Required, null,
            VolatilityFreshnessStatus.Unavailable)).PrimaryReasonCode.Should().Be("TS.VOL.REQUIRED.MISSING");
        Evaluate(WithVolatility(baseline, VolatilityDependencyRequirement.Required, Snapshot(baseline),
            VolatilityFreshnessStatus.Stale)).PrimaryReasonCode.Should().Be("TS.VOL.REQUIRED.STALE");
        Evaluate(WithVolatility(baseline, VolatilityDependencyRequirement.Required,
            Snapshot(baseline) with { IvRank = null, RankStatus = VolatilityMetricStatus.UndefinedRange },
            VolatilityFreshnessStatus.Accepted)).PrimaryReasonCode.Should().Be("TS.VOL.REQUIRED.UNQUALIFIED");
    }

    [Fact]
    public async Task Optional_missing_evidence_remains_unavailable_and_is_never_fabricated_as_zero()
    {
        var command = WithVolatility(await TradeSelectionFixture.Command(),
            VolatilityDependencyRequirement.Optional, null, VolatilityFreshnessStatus.Unavailable);

        var result = Evaluate(command);

        result.Outcome.Should().Be(SelectionOutcome.Selected);
        result.DecisionContext.VolatilityInput!.Snapshot.Should().BeNull();
        result.AcceptedVolatilityEvidence.Should().BeNull();
        result.GlobalEvidence.Single(x => x.RuleId == "G22").Status.Should().Be(SelectionRuleStatus.Passed);
        result.GlobalEvidence.Single(x => x.RuleId == "G22").ActualJson.Should().Contain("Unavailable").And.NotContain(":0");
    }

    [Fact]
    public async Task Qualified_snapshot_is_copied_exactly_and_survives_serialization()
    {
        var baseline = await TradeSelectionFixture.Command();
        var snapshot = Snapshot(baseline);
        var command = WithVolatility(baseline, VolatilityDependencyRequirement.Required, snapshot,
            VolatilityFreshnessStatus.Accepted);

        var result = Evaluate(command);
        var restored = MessagePackBinarySerializer.Shared.Deserialize<TradeSelectionResult>(
            MessagePackBinarySerializer.Shared.Serialize(result))!;

        result.Outcome.Should().Be(SelectionOutcome.Selected);
        result.AcceptedVolatilityEvidence!.SnapshotId.Should().Be(snapshot.SnapshotId);
        result.AcceptedVolatilityEvidence.SnapshotDigest.Should().Be(snapshot.SnapshotDigest);
        result.AcceptedVolatilityEvidence.Series.Should().Be(snapshot.Series);
        result.AcceptedVolatilityEvidence.MetricPolicyVersion.Should().Be(snapshot.MetricPolicyVersion);
        result.AcceptedVolatilityEvidence.RuleOutcomeCode.Should().Be("VOL.ACCEPTED");
        restored.DecisionContext.VolatilityInput.Should().BeEquivalentTo(result.DecisionContext.VolatilityInput);
        restored.AcceptedVolatilityEvidence.Should().Be(result.AcceptedVolatilityEvidence);
    }

    [Fact]
    public async Task Rank_does_not_override_an_independent_no_new_trade_rule()
    {
        var baseline = await TradeSelectionFixture.Command();
        baseline = TradeSelectionTestInputs.Evidence(baseline,
            regimeChange: decision => TradeSelectionTestInputs.Set(decision, nameof(decision.Direction),
                Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model.RegimeDirection.Down));
        var result = Evaluate(WithVolatility(baseline, VolatilityDependencyRequirement.Required,
            Snapshot(baseline) with { IvRank = 100m }, VolatilityFreshnessStatus.Accepted));

        result.Outcome.Should().Be(SelectionOutcome.NoTrade);
        result.PrimaryReasonCode.Should().Be("TS.NO_COMPATIBLE_CANDIDATE");
    }

    [Fact]
    public void Protective_closing_and_cancel_actions_do_not_depend_on_analytics_availability()
    {
        var input = Input(DateTime.UtcNow, VolatilityDependencyRequirement.Required, null,
            VolatilityFreshnessStatus.Unavailable);

        VolatilityWorkflowGate.Evaluate(input).AllowsNewEntry.Should().BeFalse();
        VolatilityWorkflowGate.Evaluate(input, protectiveOrClosingAction: true)
            .Should().Be(new VolatilityWorkflowGateResult(true, "VOL.PROTECTIVE_ACTION"));
    }

    [Fact]
    public void MarketCondition_and_RegimeDiscovery_contract_surfaces_do_not_acquire_option_volatility_evidence()
    {
        var unchangedTypes = new[]
        {
            typeof(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.HorizonAssessment),
            typeof(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model.RegimeDiscoveryDecision)
        };

        unchangedTypes.SelectMany(x => x.GetProperties())
            .Should().NotContain(property =>
                property.PropertyType.Namespace != null
                && property.PropertyType.Namespace.Contains("OptionVolatility", StringComparison.Ordinal)
                || property.Name.Contains("IvRank", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("IvPercentile", StringComparison.OrdinalIgnoreCase));
    }

    static TradeSelectionResult Evaluate(ExecuteTradeSelectionPipelineCommand command) =>
        new TradeSelectionEvaluator().Calculate(command);

    internal static ExecuteTradeSelectionPipelineCommand WithVolatility(ExecuteTradeSelectionPipelineCommand command,
        VolatilityDependencyRequirement requirement, OptionIvMetricSnapshot? snapshot,
        VolatilityFreshnessStatus freshness)
    {
        var input = Input(command.EvaluatedAtUtc, requirement, snapshot, freshness);
        return TradeSelectionTestInputs.Bind(command, command.SelectionBinding with { VolatilityInput = input });
    }

    static VolatilityWorkflowInput Input(DateTime atUtc, VolatilityDependencyRequirement requirement,
        OptionIvMetricSnapshot? snapshot, VolatilityFreshnessStatus freshness) => new()
    {
        Dependency = new(VolatilityWorkflowDependencyPolicy.CurrentSchemaVersion,
            "ES-selection-volatility", "dependency-v1", new("ES-ATM-30D", "method-v1"),
            "metric-v1", requirement),
        Snapshot = snapshot,
        FreshnessStatus = freshness,
        EvaluatedAtUtc = new DateTimeOffset(DateTime.SpecifyKind(atUtc, DateTimeKind.Utc)),
        QualificationReasonCode = snapshot is null ? "VOL.UNAVAILABLE" : "VOL.CANDIDATE"
    };

    internal static OptionIvMetricSnapshot Snapshot(ExecuteTradeSelectionPipelineCommand command)
    {
        var at = new DateTimeOffset(DateTime.SpecifyKind(command.EvaluatedAtUtc, DateTimeKind.Utc));
        return new(OptionIvMetricSnapshot.CurrentSchemaVersion, "snapshot-selection", new string('d', 64),
            new("ES-ATM-30D", "method-v1"), "metric-v1", DateOnly.FromDateTime(command.EvaluatedAtUtc),
            "daily-close", 0.25m, VolatilityValueUnit.AnnualDecimal, 75m, VolatilityMetricStatus.Qualified,
            80m, VolatilityMetricStatus.Qualified, VolatilityMetricUnit.PercentagePoints0To100,
            0.10m, 0.30m, 8, 0, 10, 10, 1m, DateOnly.FromDateTime(command.EvaluatedAtUtc).AddDays(-14),
            DateOnly.FromDateTime(command.EvaluatedAtUtc).AddDays(-1), ["observation-1"], new string('s', 64),
            "calculator-v1", at.AddMinutes(-3), at.AddMinutes(-2), at.AddMinutes(-1), at.AddSeconds(-30));
    }
}
