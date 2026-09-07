using FluentAssertions;
using System.Globalization;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using static TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection.TradeSelectionTestInputs;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
[Trait("Gate","TS-04")]
public sealed class TradeSelectionRankingTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task Multiple_deployments_rank_by_priority_then_canonical_ID_and_survive_permutations(int priority)
    {
        var c=Second(await TradeSelectionFixture.Command(),priority);var r=TradeSelectionEvaluator.Evaluate(c);
        var expected=c.SelectionBinding.Candidates.OrderBy(x=>x.AssignmentPriority).ThenBy(x=>x.DeploymentKey.Id.ToString("D"),StringComparer.Ordinal).First();
        r.SelectedCandidate!.CandidateHash.Should().Be(expected.CandidateHash);
        r.CandidateDecisions.Should().ContainSingle(x=>x.Status==SelectionCandidateStatus.EligibleNotSelected && x.ReasonCodes.Contains("TS.RANK.LOWER_PREFERENCE"));
        var culture=CultureInfo.CurrentCulture;
        try
        {
            foreach(var name in new[]{"tr-TR","en-CA","fr-CA"})
            {
                CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(name);var b=c.SelectionBinding;
                var permuted=Bind(c,b with {Candidates=b.Candidates.Reverse().ToArray(),CatalogDefinitions=b.CatalogDefinitions.Reverse().ToArray(),DeploymentSnapshots=b.DeploymentSnapshots.Reverse().ToArray(),PipelinePolicies=b.PipelinePolicies.Reverse().ToArray()});
                TradeSelectionContracts.WireHash(TradeSelectionEvaluator.Evaluate(permuted)).Should().Be(TradeSelectionContracts.WireHash(r));
            }
        }
        finally{CultureInfo.CurrentCulture=culture;}
    }
    [Fact]
    public void Fund_priority_precedes_variant_preference_then_every_tie_component()
    {
        var key=new CatalogKey(StrategyCatalogKind.Deployment,Guid.Parse("10000000-0000-0000-0000-000000000000"),1);
        var a=new SelectionComparisonTuple {Priority=1,Preference=999,Deployment=key,Strategy=key,Structure=key,Variant=key,ProductId=1,AssignmentVersion=1};
        var compare=TradeSelectionEvaluator.ComparisonComparer.Instance;
        compare.Compare(a,a with {Priority=2,Preference=0}).Should().BeNegative();
        foreach(var other in new[]{a with {Preference=1000},a with {Deployment=key with {Version=2}},a with {Strategy=key with {Version=2}},a with {Structure=key with {Version=2}},a with {Variant=key with {Version=2}},a with {ProductId=2},a with {AssignmentVersion=2}})
            compare.Compare(a,other).Should().BeNegative();
    }
    internal static ExecuteTradeSelectionPipelineCommand Second(ExecuteTradeSelectionPipelineCommand c,int priority)
    {
        var b=c.SelectionBinding;var original=b.DeploymentSnapshots.Single();var node=b.CatalogDefinitions.Single(x=>x.Key==original.DeploymentKey);
        var source=SelectionCatalogTransport.ToSource(node);var key=new CatalogKey(StrategyCatalogKind.Deployment,Guid.Parse("00000001-0000-0000-0000-000000000001"),1);
        var definition=source.Definition with {Key=key,Code="SecondDeployment",Name="Second deployment"};source=source with {Definition=definition,ContentHash=StrategyCatalogValidation.ContentHash(definition)};
        var nodes=b.CatalogDefinitions.Append(SelectionCatalogTransport.From(source)).ToArray();
        var graph=original with {DeploymentKey=key,DefinitionKeys=original.DefinitionKeys.Where(x=>x!=original.DeploymentKey).Append(key).ToArray()};
        graph=graph with {ContentHash=SelectionCatalogTransport.GraphHash(key,graph.DefinitionKeys.Select(k=>SelectionCatalogTransport.ToSource(nodes.Single(x=>x.Key==k))))};
        var candidate=b.Candidates.Single() with {DeploymentKey=key,AssignmentVersion=2,AssignmentPriority=priority,CandidateHash=""};candidate=candidate with {CandidateHash=TradeSelectionContracts.CandidateHash(candidate,graph)};
        var authority=b.PortfolioSnapshot;var permission=new TradeStrategyFamilyReference(0,0){CatalogDeployment=key};
        var assignment=authority.Assignments.Single() with {TradeTemplateId=key.Id,TradeStrategyFamily=permission,AssignmentVersion=2,Priority=priority};
        authority=authority with {Assignments=[..authority.Assignments,assignment],Fund=authority.Fund with {PermittedTradeStrategyFamilies=[..authority.Fund.PermittedTradeStrategyFamilies,permission]},
            FinancialPolicy=authority.FinancialPolicy with {TradeFamilyLimits=[..authority.FinancialPolicy.TradeFamilyLimits,authority.FinancialPolicy.TradeFamilyLimits.Single() with {CatalogDeployment=key}]},PayloadSha256=""};
        authority=authority.DefensiveCopy();authority=authority with {PayloadSha256=PortfolioCanonicalHash.Compute(authority)};
        return Bind(c,b with {PortfolioSnapshot=authority,CatalogDefinitions=nodes,DeploymentSnapshots=[original,graph],Candidates=[..b.Candidates,candidate]});
    }
}
