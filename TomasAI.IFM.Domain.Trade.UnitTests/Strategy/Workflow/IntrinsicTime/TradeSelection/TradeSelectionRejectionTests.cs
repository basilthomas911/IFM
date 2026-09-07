using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using static TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection.TradeSelectionTestInputs;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
[Trait("Gate","TS-04")]
public sealed class TradeSelectionRejectionTests
{
    [Theory]
    [InlineData(false,.499999,false)] [InlineData(false,.5,true)] [InlineData(false,.500001,true)]
    [InlineData(true,.499999,false)] [InlineData(true,.5,true)] [InlineData(true,.500001,true)]
    public async Task Confidence_boundary_is_inclusive(bool assessment,double confidence,bool selected)
    {
        var c=await TradeSelectionFixture.Command();
        c=assessment?Evidence(c,assessmentChange:x=>Set(x,"AssessmentConfidence",(decimal)confidence)):Evidence(c,regimeChange:x=>Set(x,"Confidence",(decimal)confidence));
        var r=TradeSelectionEvaluator.Evaluate(c);r.Outcome.Should().Be(selected?SelectionOutcome.Selected:SelectionOutcome.NoTrade);
        TradeSelectionContracts.ReadResult(Envelope(r)).Should().NotBeNull();
    }
    public static IEnumerable<object[]> CategoricalFields()
    {
        foreach(var field in new[]{"Quality","TrendPhase","VolatilityLevel","VolatilityChange","StructureClassification"})yield return [false,field];
        foreach(var field in new[]{"LiquidityCondition","SessionState","EventRiskState","StressState","VolatilityBehavior","TriggerAlignment","DataQuality"})yield return [true,field];
    }
    [Theory,MemberData(nameof(CategoricalFields))]
    public async Task Unknown_categorical_observation_cannot_select(bool assessment,string field)
    {
        var c=await TradeSelectionFixture.Command();
        void Unknown<T>(T value){var t=typeof(T).GetProperty(field)!.PropertyType;var name=Enum.GetNames(t).First(x=>x is "Unknown" or "Undefined");Set(value,field,Enum.Parse(t,name));}
        c=assessment?Evidence(c,assessmentChange:Unknown):Evidence(c,regimeChange:Unknown);
        var result=TradeSelectionEvaluator.Evaluate(c);result.Outcome.Should().Be(SelectionOutcome.NoTrade);result.GlobalEvidence.Should().Contain(x=>x.ReasonCode=="TS.EVIDENCE.UNKNOWN");
    }
    [Fact]
    public async Task Required_unknown_direction_fails_upstream_validation()
    {
        var c=Evidence(await TradeSelectionFixture.Command(),regimeChange:x=>Set(x,"Direction",RegimeDirection.Unknown));
        Action evaluate=()=>TradeSelectionEvaluator.Evaluate(c);evaluate.Should().Throw<ArgumentException>();
    }
    [Theory]
    [InlineData("poor")] [InlineData("closed")] [InlineData("event")] [InlineData("data")]
    public async Task Available_but_unfavorable_context_is_explained_NoTrade(string scenario)
    {
        var c=await TradeSelectionFixture.Command();c=Evidence(c,assessmentChange:x=>
        {
            if(scenario=="poor")Set(x,"LiquidityCondition",AssessmentLiquidity.Poor);
            if(scenario=="closed")Set(x,"SessionState",MarketSessionStatus.Closed);
            if(scenario=="event")Set(x,"EventRiskState",AssessmentEventContext.Elevated);
            if(scenario=="data")Set(x,"DataQuality",MarketConditionDataQuality.Unusable);
        });
        var result=TradeSelectionEvaluator.Evaluate(c);result.Outcome.Should().Be(SelectionOutcome.NoTrade);result.CandidateDecisions.Should().OnlyContain(x=>x.Status==SelectionCandidateStatus.NotEvaluated);
    }
    [Theory]
    [InlineData("zero")] [InlineData("permissions")] [InlineData("disabled")] [InlineData("portfolio-paused")] [InlineData("fund-paused")] [InlineData("blocked")]
    public async Task No_authorized_candidate_never_creates_a_default(string scenario)
    {
        var c=await TradeSelectionFixture.Command();var a=c.SelectionBinding.PortfolioSnapshot;
        a=scenario switch
        {
            "zero"=>a with {Assignments=[]},"permissions"=>a with {Fund=a.Fund with {PermittedTradeStrategyFamilies=[]}},
            "disabled"=>a with {Assignments=a.Assignments.Select(x=>x with {Enabled=false}).ToArray()},
            "portfolio-paused"=>a with {Portfolio=a.Portfolio with {OperatingState=PortfolioOperatingState.Paused}},
            "fund-paused"=>a with {Fund=a.Fund with {OperatingState=FundOperatingState.Paused}},
            _=>a with {RiskEnvelope=a.RiskEnvelope with {CapacityState=FundCapacityState.Blocked}}
        };
        var result=TradeSelectionEvaluator.Evaluate(Authority(c,a));result.Outcome.Should().Be(SelectionOutcome.NoTrade);result.SelectedCandidate.Should().BeNull();
        TradeSelectionContracts.ReadResult(Envelope(result)).Should().NotBeNull();
    }
    [Theory]
    [InlineData("candidate-omitted")] [InlineData("graph-omitted")] [InlineData("duplicate-candidate")] [InlineData("hash")] [InlineData("schema")] [InlineData("profile")] [InlineData("future")] [InlineData("numeric")]
    public async Task Malformed_evidence_fails_without_selecting_an_alternative(string scenario)
    {
        var c=await TradeSelectionFixture.Command();var b=c.SelectionBinding;
        c=scenario switch
        {
            "candidate-omitted"=>Bind(c,b with {Candidates=[]}),
            "graph-omitted"=>Bind(c,b with {DeploymentSnapshots=[]}),
            "duplicate-candidate"=>Bind(c,b with {Candidates=[b.Candidates[0],b.Candidates[0]]}),
            "hash"=>c with {SelectionBinding=b with {PayloadSha256=new string('0',64)}},
            "schema"=>c with {SchemaVersion=0},
            "profile"=>Bind(c,b with {CommonPolicy=b.CommonPolicy with {Version=2}}),
            "future"=>c with {EvaluatedAtUtc=b.FrozenAtUtc.AddDays(-1)},
            _=>Evidence(c,regimeChange:x=>Set(x,"Direction",(RegimeDirection)255))
        };
        Action evaluate=()=>TradeSelectionEvaluator.Evaluate(c);evaluate.Should().Throw<Exception>();
    }
    [Theory]
    [InlineData("intent")] [InlineData("confidence")] [InlineData("revision")] [InlineData("evidence")] [InlineData("outcome")] [InlineData("deadline")]
    public async Task Rehashed_forged_result_is_not_valid(string scenario)
    {
        var r=TradeSelectionEvaluator.Evaluate(await TradeSelectionFixture.Command());
        r=scenario switch {"intent"=>r with {SelectedCandidate=r.SelectedCandidate! with {Side="Short"}},"confidence"=>r with {SelectionConfidence=.1m},
            "revision"=>r with {InputWorkflowRevision=0},"evidence"=>r with {GlobalEvidence=[]},"outcome"=>r with {Outcome=SelectionOutcome.NoTrade},_=>r with {ValidUntilUtc=r.EvaluatedAtUtc}};
        Action read=()=>TradeSelectionContracts.ReadResult(Envelope(r));read.Should().Throw<Exception>();
    }
}
