using System.Collections.Immutable;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;

[Trait("Category","PortfolioFinancial")]
public sealed class RiskPreparationTests
{
    [Theory]
    [InlineData(TimeFrameType.Daily)] [InlineData(TimeFrameType.Weekly)] [InlineData(TimeFrameType.Monthly)]
    public void Three_draft_profiles_have_stable_identity_strict_payloads_and_explicit_emulator_defaults(TimeFrameType horizon)
    {
        var policy=RiskParameterSet.Default(horizon);
        RiskParameterSet.Read(policy.Serialize()).Should().Be(policy);
        RiskParameterSet.Default(horizon).Hash().Should().Be(policy.Hash());
        policy.MaximumUnits.Should().Be(10); policy.PerTradeRiskFraction.Should().Be(.01m);
        policy.Environment.Should().Be("Emulator");
        var live=policy with { Environment="Live" };
        live.Invoking(x=>x.Validate()).Should().Throw<ArgumentException>();
        Action extra=()=>RiskParameterSet.Read(policy.Serialize().TrimEnd('}')+",\"IgnoredLimit\":1}");
        extra.Should().Throw<System.Text.Json.JsonException>();
    }

    [Fact]
    public async Task Emulator_quotes_use_gross_contracts_pin_account_and_candidate_and_never_supply_live_margin()
    {
        var request=await RiskFixture.Command("LongBullishIronCondor"); var candidate=request.CompositionResult.ReadCompositionResult().Candidate!;
        var policy=RiskParameterSet.Default(candidate.TargetHorizon);
        var quotes=EmulatorMarginModel.Quote(policy,candidate,"emulator/account-one","Emulator",request.EvaluatedAtUtc,request.ExpiresAtUtc);
        quotes.Should().HaveCount(10);
        // Four legs times two units: no unqualified portfolio/spread margin offset.
        quotes[1].MarginRequirement.Should().Be(200000m);
        quotes[1].EntryFees.Should().Be(40m); quotes[1].VariationReserve.Should().Be(8000m);
        var same=EmulatorMarginModel.Quote(policy,candidate,"emulator/account-one","Emulator",request.EvaluatedAtUtc,request.ExpiresAtUtc);
        same.Should().Equal(quotes);
        var other=EmulatorMarginModel.Quote(policy,candidate,"emulator/account-two","Emulator",request.EvaluatedAtUtc,request.ExpiresAtUtc);
        other[1].Evidence.ContentHash.Should().NotBe(quotes[1].Evidence.ContentHash);
        Action live=()=>EmulatorMarginModel.Quote(policy,candidate,"live/account","Live",request.EvaluatedAtUtc,request.ExpiresAtUtc);
        live.Should().Throw<RiskCalculationException>().Which.ReasonCode.Should().Be("RM.MARGIN.ENVIRONMENT");
    }

    [Fact]
    public async Task Preparation_freezes_exact_upstream_snapshots_policy_and_one_financial_revision()
    {
        var input=await Input();
        var execute=RiskPreparation.Create(input.View,input.Policy,input.Financial,Guid.NewGuid(),input.At);
        execute.InputWorkflowRevision.Should().Be(input.View.WorkflowRevision+1);
        execute.InputSha256.Should().Be(execute.Fingerprint());
        execute.ConfigurationPayloadSha256.Should().Be(input.Policy.Hash());
        execute.CompositionResult.Should().BeSameAs(input.View.OrderComposition.Result);
        execute.SizingAuthority.AvailableCash.Should().Be(1000000000);
        execute.SizingAuthority.RiskCapital.Should().Be(1000000000);
        execute.Funding.Should().HaveCount(10);
        new RiskEvaluator().Calculate(execute).Outcome.Should().Be(RiskAssessmentOutcome.Approved);
        // Source arrays cannot mutate the frozen request after preparation.
        input.Financial.Value!.Limits[0]=input.Financial.Value.Limits[0] with { Maximum=0 };
        execute.InputSha256.Should().Be(execute.Fingerprint());
    }

    [Fact]
    public async Task Per_trade_budget_does_not_reuse_the_larger_aggregate_loss_limit()
    {
        var input=await Input();
        var financial=input.Financial with { Value=input.Financial.Value! with { MaximumRiskPerTrade=500m } };
        var execute=RiskPreparation.Create(input.View,input.Policy,financial,Guid.NewGuid(),input.At);
        execute.SizingAuthority.PerTradeLossBudget.Should().Be(500m);
        execute.SizingAuthority.Limits.Where(x=>x.Measure==CapacityMeasure.LossCharge).Should().OnlyContain(x=>x.Maximum>500m);
    }

    [Theory]
    [InlineData("unqualified")] [InlineData("stale")] [InlineData("fund")] [InlineData("environment")]
    [InlineData("expired")] [InlineData("disabledLimit")] [InlineData("missingPerTradeCap")]
    public async Task Invalid_authority_cannot_be_prepared(string alteration)
    {
        var input=await Input(); var read=input.Financial; var book=read.Value!; var at=input.At;
        if(alteration=="missingPerTradeCap") book=book with { MaximumRiskPerTrade=0 };
        if(alteration=="unqualified") book=book with { MigrationQualified=false };
        if(alteration=="fund") book=book with { FundId=book.FundId+1 };
        if(alteration=="environment") book=book with { Environment="Live" };
        if(alteration=="stale") read=read with { ObservedAtUtc=at.AddSeconds(-2) };
        if(alteration=="expired") at=input.View.ExpiresAtUtc;
        if(alteration=="disabledLimit") book=book with { Limits=book.Limits.Select(x=>x.Measure==CapacityMeasure.LossCharge ? x with { Enabled=false } : x).ToArray() };
        read=read with { Value=book };
        Action prepare=()=>RiskPreparation.Create(input.View,input.Policy,read,Guid.NewGuid(),at);
        prepare.Should().Throw<RiskCalculationException>();
    }

    internal static async Task<(IntrinsicTimeStrategyWorkflowView View,RiskParameterSet Policy,FinancialRead<FinancialAdmissionSnapshot> Financial,DateTime At)> Input()
    {
        var request=await RiskFixture.Command(); var candidate=request.CompositionResult.ReadCompositionResult().Candidate!;
        var view=new IntrinsicTimeStrategyWorkflowView
        {
            EntityId=request.WorkflowEntityId,WorkflowId=request.WorkflowId,WorkflowRevision=request.InputWorkflowRevision-1,
            CorrelationId=request.CorrelationId,Status=WorkflowStrategyMachineStatus.Started,CurrentStage=StrategyWorkflowStage.RiskManagement,
            ExpiresAtUtc=request.ExpiresAtUtc,RegimeDiscovery=new() { Result=request.RegimeResult },MarketCondition=new() { Result=request.MarketConditionResult },
            TradeSelection=new() { Result=request.SelectionResult },OrderComposition=new() { Result=request.CompositionResult,SourceEventId=request.CompositionResult.ResultId },
            CompositionExecution=new() { MarketSnapshot=request.MarketSnapshot }
        };
        var book=new FinancialAdmissionSnapshot(1,candidate.PortfolioId,candidate.FundId,"Active",true,true,request.Authority,
            request.SizingAuthority.AvailableCash,request.SizingAuthority.Limits.ToArray(),[],"Emulator","emulator/account-one",request.SizingAuthority.PerTradeLossBudget);
        return(view,RiskParameterSet.Default(candidate.TargetHorizon),new(FinancialReadStatus.Found,book,1,request.EvaluatedAtUtc),request.EvaluatedAtUtc);
    }
}
