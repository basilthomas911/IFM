using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
public sealed class TradeSelectionEvaluationTests
{
    public static IEnumerable<object[]> Variants()=>from v in StrategyCatalogExamples.Create().Where(x=>x.Key.Kind==StrategyCatalogKind.Variant) from h in new[]{TimeFrameType.Daily,TimeFrameType.Weekly,TimeFrameType.Monthly} select new object[]{v.Code,h};
    [Theory,MemberData(nameof(Variants)),Trait("Fixture","TS-C01")]
    public async Task Every_supported_variant_selects_on_each_triggering_horizon(string variant,TimeFrameType horizon)
    {
        var command=await TradeSelectionFixture.Command(variant,horizon);
        var result=TradeSelectionEvaluator.Evaluate(command);
        result.Outcome.Should().Be(SelectionOutcome.Selected);result.DecisionHorizon.Should().Be(horizon);
        result.SelectedCandidate!.VariantKey.Should().Be(command.SelectionBinding.CatalogDefinitions.Single(x=>x.Code==variant).Key);
        result.GlobalEvidence.Should().HaveCount(21);result.CandidateDecisions.Single().RuleEvidence.Should().HaveCount(9);
        result.GlobalEvidence.Should().OnlyContain(x=>x.Status==SelectionRuleStatus.Passed);
        result.SelectedCandidate.CompositionPolicyReference.Should().Be(command.SelectionBinding.Candidates.Single().CompositionPolicyReference);
        var wire=MessagePackSerializer.Deserialize<Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.ExecuteTradeSelectionPipelineCommand>(MessagePackSerializer.Serialize(command));
        TradeSelectionContracts.WireHash(TradeSelectionEvaluator.Evaluate(wire)).Should().Be(TradeSelectionContracts.WireHash(result));
        var persisted = Newtonsoft.Json.JsonConvert.DeserializeObject<Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.ExecuteTradeSelectionPipelineCommand>(
            Newtonsoft.Json.JsonConvert.SerializeObject(command))!;
        TradeSelectionContracts.ValidateRequest(persisted);
        TradeSelectionContracts.EvidenceHash(TradeSelectionEvaluator.Evaluate(persisted)).Should().Be(TradeSelectionContracts.EvidenceHash(result));
        persisted.Fingerprint().Should().Be(command.Fingerprint());
    }
    [Fact,Trait("Fixture","TS-C25")]
    public async Task Complete_catalog_roundtrip_preserves_original_source_hashes()
    {
        var c=await TradeSelectionFixture.Command("LongBalancedIronCondor");
        foreach(var node in c.SelectionBinding.CatalogDefinitions)
        {
            var copy=MessagePackSerializer.Deserialize<SelectionCatalogDefinitionSnapshot>(MessagePackSerializer.Serialize(node));
            Application.Storage.ConfigurationDb.StrategyCatalog.StrategyCatalogValidation.ContentHash(SelectionCatalogTransport.ToSource(copy).Definition).Should().Be(node.ContentHash);
        }
    }
}
