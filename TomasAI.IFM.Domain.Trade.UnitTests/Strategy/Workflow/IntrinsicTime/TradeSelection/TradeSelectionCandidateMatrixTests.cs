using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using static TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection.TradeSelectionTestInputs;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
[Trait("Gate","TS-04")]
public sealed class TradeSelectionCandidateMatrixTests
{
    public static IEnumerable<object[]> Rejections()
    {
        var common=TradeSelectionDefaultProfiles.Create(Guid.NewGuid(),TimeFrameType.Daily);var examples=StrategyCatalogExamples.Create();
        foreach(var variant in examples.Where(x=>x.Key.Kind==StrategyCatalogKind.Variant))
        {
            var builder=examples.Single(x=>x.Key==variant.Parent).Capabilities.Single(x=>x.Role=="builder").Code;
            var rule=common.VariantRules.Single(x=>x.BuilderCapabilityCode==builder && x.Side==variant.Side && x.Bias==variant.Bias && x.PremiumMode==variant.PremiumMode);
            foreach(var field in new[]{("AllowedRegimeDirections","Direction","C04",false),("AllowedTrendPhases","TrendPhase","C05",false),("AllowedTrendStrengths","TrendStrength","C06",false),("AllowedStructureClassifications","StructureClassification","C07",false),("AllowedAssessmentConditions","ConditionType","C08",true),("AllowedVolatilityBehavior","VolatilityBehavior","C09",true)})
            {
                var allowed=((Array)typeof(TradeSelectionParameterSet).GetProperty(field.Item1)!.GetValue(common)!).Cast<object>();
                var compatible=((Array)typeof(SelectionVariantRule).GetProperty(field.Item1)!.GetValue(rule)!).Cast<object>();
                foreach(var denied in allowed.Except(compatible))yield return [variant.Code,field.Item2,denied,field.Item3,field.Item4];
            }
        }
    }
    [Theory,MemberData(nameof(Rejections))]
    public async Task Every_categorical_variant_mismatch_has_a_specific_rejection(string variant,string field,object value,string rule,bool assessment)
    {
        var c=await TradeSelectionFixture.Command(variant);
        c=assessment?Evidence(c,assessmentChange:x=>Set(x,field,value)):Evidence(c,regimeChange:x=>Set(x,field,value));
        var result=TradeSelectionEvaluator.Evaluate(c);result.Outcome.Should().Be(SelectionOutcome.NoTrade);
        result.CandidateDecisions.Single().RuleEvidence.Should().Contain(x=>x.RuleId==rule && x.Status==SelectionRuleStatus.Rejected);
    }
}
