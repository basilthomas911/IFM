using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
[Trait("Gate","TS-03")]
public sealed class PortfolioSelectionResolverTests
{
    [Fact]
    public async Task Selector_retains_option_assignments_for_a_futures_trigger_and_zero_assignments()
    {
        var c=await TradeSelectionFixture.Command("BullCallDebit");var a=c.SelectionBinding.PortfolioSnapshot;
        var selected=Resolve(a,a.Assignments);selected.Assignments.Should().ContainSingle(x=>x.AssetType=="FuturesOptions");
        Resolve(a,[]).Assignments.Should().BeEmpty();
        Action strict=()=>new PortfolioFundStrategyResolver().Resolve(a.WorkflowId,1,a.CorrelationId,a.Portfolio,a.FinancialPolicy,[a.Fund],[a.Allocation],[a.RiskEnvelope],a.Assignments,a.Fund.TradingYear,a.Fund.DecisionHorizon,"ES","Futures",a.ResolvedAtUtc);
        strict.Should().Throw<PortfolioResolutionException>();
    }
    [Theory]
    [InlineData(16,false)] [InlineData(17,true)]
    public async Task Assignment_limit_includes_disabled_rows_and_never_truncates(int count,bool fail)
    {
        var a=(await TradeSelectionFixture.Command()).SelectionBinding.PortfolioSnapshot;
        var assignments=Enumerable.Range(1,count).Select(i=>{var id=Guid.NewGuid();return a.Assignments[0] with {AssignmentVersion=i,Enabled=false,TradeTemplateId=id,TradeStrategyFamily=new TradeStrategyFamilyReference(0,0){CatalogDeployment=new(StrategyCatalogKind.Deployment,id,1)}};}).ToArray();
        if(fail){Action resolve=()=>Resolve(a,assignments);resolve.Should().Throw<PortfolioResolutionException>().WithMessage("*sixteen*");}
        else Resolve(a,assignments).Assignments.Should().HaveCount(count);
    }
    [Fact]
    public async Task Ambiguous_fund_and_overlapping_exact_assignment_fail_distinctly()
    {
        var a=(await TradeSelectionFixture.Command()).SelectionBinding.PortfolioSnapshot;
        Action duplicate=()=>Resolve(a,[a.Assignments[0],a.Assignments[0] with {AssignmentVersion=2}]);duplicate.Should().Throw<PortfolioResolutionException>().WithMessage("*more than one effective assignment*");
        Action funds=()=>new PortfolioFundStrategyResolver().ResolveForSelection(a.WorkflowId,1,a.CorrelationId,a.Portfolio,a.FinancialPolicy,[a.Fund,a.Fund with {FundId=2}],[a.Allocation],[a.RiskEnvelope],a.Assignments,a.Fund.TradingYear,a.Fund.DecisionHorizon,"ES",a.ResolvedAtUtc);
        funds.Should().Throw<PortfolioResolutionException>();
    }
    static PortfolioFundStrategySnapshot Resolve(PortfolioFundStrategySnapshot a,FundTradeTemplateAssignmentReadModel[] assignments)=>new PortfolioFundStrategyResolver().ResolveForSelection(a.WorkflowId,1,a.CorrelationId,a.Portfolio,a.FinancialPolicy,[a.Fund],[a.Allocation],[a.RiskEnvelope],assignments,a.Fund.TradingYear,a.Fund.DecisionHorizon,"ES",a.ResolvedAtUtc,a.Fund.FundId);
}
