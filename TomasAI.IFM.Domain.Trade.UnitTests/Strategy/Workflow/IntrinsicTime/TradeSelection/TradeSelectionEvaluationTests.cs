using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;

public sealed class TradeSelectionEvaluationTests
{
    [Theory]
    [InlineData("LongFuture", TimeFrameType.Daily)]
    [InlineData("LongBalancedIronCondor", TimeFrameType.Monthly)]
    public async Task Ema_policy_keeps_selection_working_without_inventing_legacy_phase_or_volatility(string variant, TimeFrameType horizon)
    {
        var command = await TradeSelectionFixture.Command(variant, horizon);
        var regime = command.RegimeResultEnvelope.ReadRegimeResult();
        regime = regime with
        {
            Reasons = [new() { Code = Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model.EmaStackRegimePolicy.ReasonCode }],
            Decision = regime.Decision with
            {
                TrendPhase = Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model.TrendRegimePhase.Unknown,
                VolatilityLevel = Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model.VolatilityRegimeLevel.Unknown,
                VolatilityChange = Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model.VolatilityRegimeChange.Unknown
            }
        };
        command = command with { RegimeResultEnvelope = Shared.Strategy.Workflow.IntrinsicTime.Model.StrategyStageResultEnvelope.CreateRegime(regime) };
        command = TradeSelectionTestInputs.Evidence(command);
        var result = TradeSelectionEvaluator.Evaluate(command);
        result.Outcome.Should().Be(SelectionOutcome.Selected);
        result.GlobalEvidence.Should().Contain(x => x.RuleId == "G10" && x.Status == SelectionRuleStatus.Passed && x.ActualJson.Contains("NotRequired"));
        result.GlobalEvidence.Should().Contain(x => x.RuleId == "G17" && x.Status == SelectionRuleStatus.Passed);
        var validated = TradeSelectionContracts.ReadResult(Shared.Strategy.Workflow.IntrinsicTime.Model.StrategyStageResultEnvelope.CreateSelection(result));
        validated.Outcome.Should().Be(SelectionOutcome.Selected);
    }

    public static IEnumerable<object[]> Variants() => from v in StrategyCatalogExamples.Create().Where(x => x.Key.Kind == StrategyCatalogKind.Variant) from h in new[] { TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly } select new object[] { v.Code, h };
    [Theory, MemberData(nameof(Variants)), Trait("Fixture", "TS-C01")]
    public async Task Every_supported_variant_selects_on_each_triggering_horizon(string variant, TimeFrameType horizon)
    {
        var command = await TradeSelectionFixture.Command(variant, horizon);
        var result = TradeSelectionEvaluator.Evaluate(command);
        result.Outcome.Should().Be(SelectionOutcome.Selected); result.DecisionHorizon.Should().Be(horizon);
        result.SelectedCandidate!.VariantKey.Should().Be(command.SelectionBinding.CatalogDefinitions.Single(x => x.Code == variant).Key);
        result.GlobalEvidence.Should().HaveCount(21); result.CandidateDecisions.Single().RuleEvidence.Should().HaveCount(9);
        result.GlobalEvidence.Should().OnlyContain(x => x.Status == SelectionRuleStatus.Passed);
        result.SelectedCandidate.CompositionPolicyReference.Should().Be(command.SelectionBinding.Candidates.Single().CompositionPolicyReference);
        var wire = MessagePackSerializer.Deserialize<Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.ExecuteTradeSelectionPipelineCommand>(MessagePackSerializer.Serialize(command));
        TradeSelectionContracts.WireHash(TradeSelectionEvaluator.Evaluate(wire)).Should().Be(TradeSelectionContracts.WireHash(result));
        var persisted = Newtonsoft.Json.JsonConvert.DeserializeObject<Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.ExecuteTradeSelectionPipelineCommand>(
            Newtonsoft.Json.JsonConvert.SerializeObject(command))!;
        TradeSelectionContracts.ValidateRequest(persisted);
        TradeSelectionContracts.EvidenceHash(TradeSelectionEvaluator.Evaluate(persisted)).Should().Be(TradeSelectionContracts.EvidenceHash(result));
        persisted.Fingerprint().Should().Be(command.Fingerprint());
    }
    [Fact, Trait("Fixture", "TS-C25")]
    public async Task Complete_catalog_roundtrip_preserves_original_source_hashes()
    {
        var c = await TradeSelectionFixture.Command("LongBalancedIronCondor");
        foreach (var node in c.SelectionBinding.CatalogDefinitions)
        {
            var copy = MessagePackSerializer.Deserialize<SelectionCatalogDefinitionSnapshot>(MessagePackSerializer.Serialize(node));
            Domain.Strategy.Contracts.Shared.Canonical.StrategyCatalogValidation.ContentHash(SelectionCatalogTransport.ToSource(copy).Definition).Should().Be(node.ContentHash);
        }
    }
}
