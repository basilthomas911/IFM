using System.Diagnostics;
using MessagePack;
using NSubstitute;
using Xunit.Abstractions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.Runtime;

/// <summary>Independent downstream calculation qualification; these frozen accepted inputs do not imply the current EMA options gate approves.</summary>
[Collection(WorkflowDecisionQualificationCollection.Name)]
public sealed class WorkflowFrozenOptionsCalculationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("BearPutDebit",TimeFrameType.Weekly)]
    [InlineData("ShortBalancedIronCondor",TimeFrameType.Monthly)]
    public async Task Missing_published_option_chain_returns_one_terminal_capture_without_fetch_or_retry(string variant,TimeFrameType horizon)
    {
        var at=new DateTime(2026,11,20,15,0,1,DateTimeKind.Utc);
        var command=await OrderComposer.CompositionFixture.Command(variant,horizon,atUtc:at,cacheDefaults:true);
        var clock=new AdvancingClock(command.EvaluatedAtUtc);
        var market=Substitute.For<TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi>();
        var routes=Substitute.For<TomasAI.IFM.Application.MarketData.Pricing.ICompositionRoutePlanStore>();
        using var chain=new TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCache(clock);
        market.OptionChainCache.Returns(chain);
        var cache=new WorkflowCompositionInputCache(market,routes,new TomasAI.IFM.Application.MarketData.Databento.Resiliency.DatasetWorkerAdmissionRegistry(),clock);
        await cache.PrepareAsync(command.SelectionBinding,default);
        market.ClearReceivedCalls();routes.ClearReceivedCalls();
        var view=command.WorkflowView with{DecisionRuntimeVersion=1};
        var timer=Stopwatch.StartNew();
        var first=await cache.CaptureAsync(view,DateOnly.FromDateTime(at));
        timer.Stop();
        Assert.NotNull(first.Failure);Assert.Null(first.Preparation);
        Assert.Same(first,await cache.CaptureAsync(view,DateOnly.FromDateTime(at)));
        Assert.Empty(routes.ReceivedCalls());
        Assert.Single(market.ReceivedCalls());
        Assert.All(market.ReceivedCalls(),call=>Assert.Equal("get_OptionChainCache",call.GetMethodInfo().Name));
        Assert.InRange(timer.Elapsed.TotalMilliseconds,0,1999.999);
        output.WriteLine($"MissingOptionChain;Horizon={horizon};Ms={timer.Elapsed.TotalMilliseconds:F3};Reason={first.Failure.Code};CacheReads=1;InputFetches=0;Replay=OriginalTerminal");
    }

    [Theory]
    [InlineData("BearPutDebit",TimeFrameType.Weekly)]
    [InlineData("ShortBalancedIronCondor",TimeFrameType.Monthly)]
    public async Task Frozen_valid_options_composition_and_risk_finish_once_without_input_io(string variant,TimeFrameType horizon)
    {
        var at=new DateTime(2026,11,20,15,0,1,DateTimeKind.Utc);
        var command=await OrderComposer.CompositionFixture.Command(variant,horizon,atUtc:at);
        var configuration=new PreparedCompositionConfiguration(command.SelectionBinding,command.CompositionBinding.Selected);
        var composition=new CompositionCalculation(configuration);
        var expectedComposition=composition.Model.CalculatePrepared(command,configuration);
        Assert.NotNull(expectedComposition.Candidate);
        var riskCommand=await RiskManager.RiskFixture.Command(variant,atUtc:expectedComposition.Candidate.EvaluatedAtUtc,horizon:horizon,
            actualCompositionCommand:command,actualCompositionResult:expectedComposition);
        var risk=new RiskCalculation();var expectedRisk=risk.Model.Calculate(riskCommand);
        var clock=new AdvancingClock(at);
        await using var executor=new WorkflowDecisionExecutor(timeProvider:clock);
        clock.Start();
        var timer=Stopwatch.StartNew();
        var composed=await executor.ExecuteAsync(composition,command,at,at.AddSeconds(2));
        var compositionMs=timer.Elapsed.TotalMilliseconds;
        Assert.Equal(DecisionExecutionStatus.Completed,composed.Status);
        var riskAt=clock.GetUtcNow().UtcDateTime;
        timer.Restart();
        var assessed=await executor.ExecuteAsync(risk,riskCommand,riskAt,riskAt.AddSeconds(2));
        var riskMs=timer.Elapsed.TotalMilliseconds;
        Assert.Equal(DecisionExecutionStatus.Completed,assessed.Status);
        Assert.Equal(MessagePackSerializer.Serialize(expectedComposition),MessagePackSerializer.Serialize(composed.Value));
        Assert.Equal(MessagePackSerializer.Serialize(expectedRisk),MessagePackSerializer.Serialize(assessed.Value));
        Assert.Equal(1,composition.Calls);Assert.Equal(1,risk.Calls);
        Assert.InRange(compositionMs,0,1999.999);Assert.InRange(riskMs,0,1999.999);
        output.WriteLine($"FrozenDownstream;Horizon={horizon};CompositionMs={compositionMs:F3};RiskMs={riskMs:F3};CompositionCalls={composition.Calls};RiskCalls={risk.Calls};InputIoCapabilities=None");
    }
    sealed class CompositionCalculation(PreparedCompositionConfiguration configuration):IWorkflowDecisionOperator<ExecuteOrderCompositionPipelineCommand,OrderCompositionResult>
    {
        public readonly Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer Model=new(new Black76ComposerPricer());
        public int Calls;
        public ValueTask<OrderCompositionResult> CalculateAsync(ExecuteOrderCompositionPipelineCommand input,CancellationToken token)
        {Interlocked.Increment(ref Calls);return ValueTask.FromResult(Model.CalculatePrepared(input,configuration,token));}
    }
    sealed class RiskCalculation:IWorkflowDecisionOperator<ExecuteRiskManagementPipelineCommand,RiskAssessmentResult>
    {
        public readonly RiskEvaluator Model=new();public int Calls;
        public ValueTask<RiskAssessmentResult> CalculateAsync(ExecuteRiskManagementPipelineCommand input,CancellationToken token)
        {Interlocked.Increment(ref Calls);return ValueTask.FromResult(Model.Calculate(input,token));}
    }
    sealed class AdvancingClock(DateTime at):TimeProvider
    {
        long started;
        public void Start()=>started=Stopwatch.GetTimestamp();
        public override DateTimeOffset GetUtcNow()=>new DateTimeOffset(at).Add(started==0?TimeSpan.Zero:Stopwatch.GetElapsedTime(started));
    }
}
