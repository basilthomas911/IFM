using System.Collections.Immutable;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;

public sealed class VolatilityEvidencePropagationTests
{
    [Fact]
    public async Task Accepted_snapshot_is_not_replaced_between_selection_composer_and_risk()
    {
        var selectionCommand = await TradeSelectionFixture.Command(compositionReady: true, atUtc: DateTime.UtcNow,
            compositionIntegrationTiming: true, compositionLifetimeMilliseconds: 30000);
        var snapshot = VolatilityEvidenceConsumerTests.Snapshot(selectionCommand);
        selectionCommand = VolatilityEvidenceConsumerTests.WithVolatility(selectionCommand,
            VolatilityDependencyRequirement.Required, snapshot, VolatilityFreshnessStatus.Accepted);
        var selection = new TradeSelectionEvaluator().Calculate(selectionCommand);
        var compositionCommand = await CompositionFixture.Command(actualSelectionCommand: selectionCommand,
            actualSelectionResult: selection, integrationTiming: true, integrationWindowMilliseconds: 30000);

        var composition = new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer(
            new Black76ComposerPricer()).Calculate(compositionCommand);
        var riskCommand = await RiskFixture.Command(actualCompositionCommand: compositionCommand,
            actualCompositionResult: composition);
        var risk = new RiskEvaluator().Calculate(riskCommand);

        composition.DecisionContext.VolatilityEvidence.Should().Be(selection.DecisionContext.VolatilityInput);
        composition.Candidate!.VolatilityEvidence.Should().Be(selection.DecisionContext.VolatilityInput);
        composition.DecisionContext.VolatilityEvidence!.Snapshot!.SnapshotId.Should().Be(snapshot.SnapshotId);
        composition.DecisionContext.VolatilityEvidence.Snapshot.SnapshotDigest.Should().Be(snapshot.SnapshotDigest);
        risk.VolatilityEvidence.Should().Be(selection.DecisionContext.VolatilityInput);
    }

    [Fact]
    public async Task Missing_optional_feature_is_recorded_unavailable_and_never_evaluated_as_zero()
    {
        var command = await CompositionFixture.Command("BullCallDebit");
        var sourceRule = command.CompositionBinding.Rules.VariantRules.Single();
        var baseline = sourceRule.BaseParameters.TargetLegDelta;
        var bound = new CompositionParameterBound
        {
            Parameter = CompositionParameter.TargetLegDelta,
            Minimum = Math.Max(.01m, baseline - .1m),
            Maximum = Math.Min(1m, baseline + .1m),
            Grid = .01m
        };
        var rule = sourceRule with
        {
            HardBounds = sourceRule.HardBounds.Add(bound),
            AdjustmentRules =
            [
                new CompositionAdjustment
                {
                    Code = "S4-IvRank-TargetLegDelta-v1",
                    Priority = 100,
                    Predicate = new CompositionPredicate
                    {
                        Comparison = CompositionComparison.Equal,
                        Feature = CompositionFeature.IvRank,
                        Values = [0m],
                        Required = false
                    },
                    Parameter = CompositionParameter.TargetLegDelta,
                    Operation = CompositionOperation.Set,
                    Operand = bound.Minimum
                }
            ]
        };

        var resolved = CompositionParameterResolver.Resolve(rule,
            new Dictionary<CompositionFeature, decimal>(), CancellationToken.None);

        resolved.Values.TargetLegDelta.Should().Be(sourceRule.BaseParameters.TargetLegDelta);
        resolved.Evidence.Single(x => x.Code == "S4-IvRank-TargetLegDelta-v1").Status
            .Should().Be("InputUnavailable");
    }
}
