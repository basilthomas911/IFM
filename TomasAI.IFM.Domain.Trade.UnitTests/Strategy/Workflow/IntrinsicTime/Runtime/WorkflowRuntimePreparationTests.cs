using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.Runtime;

public sealed class WorkflowRuntimePreparationTests
{
    [Fact]
    public async Task Startup_preparation_is_single_flight_and_never_marks_ready_before_completion()
    {
        var completion=new TaskCompletionSource<WorkflowRuntimePreparation.Result>(TaskCreationOptions.RunContinuationsAsynchronously);var calls=0;
        var preparation=new WorkflowRuntimePreparation(_=>{Interlocked.Increment(ref calls);return completion.Task;});
        var attempts=Enumerable.Range(0,32).Select(_=>preparation.PrepareAsync()).ToArray();
        Assert.Equal(1,calls);Assert.False(preparation.IsReady);Assert.Throws<InvalidOperationException>(preparation.RequireReady);
        completion.SetResult(new(1,1,TimeSpan.Zero));await Task.WhenAll(attempts);
        Assert.True(preparation.IsReady);preparation.RequireReady();Assert.Equal(1,calls);
    }
    [Fact]
    public async Task Startup_failure_remains_unready_and_is_not_retried_inside_a_decision()
    {
        var calls=0;var preparation=new WorkflowRuntimePreparation(_=>{Interlocked.Increment(ref calls);return Task.FromException<WorkflowRuntimePreparation.Result>(new IOException("formatter unavailable"));});
        await Assert.ThrowsAsync<IOException>(()=>preparation.PrepareAsync());
        await Assert.ThrowsAsync<IOException>(()=>preparation.PrepareAsync());
        Assert.False(preparation.IsReady);Assert.Equal("formatter unavailable",preparation.Failure);
        Assert.Throws<InvalidOperationException>(preparation.RequireReady);Assert.Equal(1,calls);
        await using var executor=new WorkflowDecisionExecutor();var runtime=new WorkflowCalculationRuntime(executor,preparation:preparation);
        Assert.False(runtime.IsPrepared);
    }
    [Fact]
    public async Task Unready_production_runtime_never_invokes_a_calculator_or_waits_for_preparation()
    {
        var preparation=new WorkflowRuntimePreparation(_=>throw new InvalidOperationException("A decision must never initiate preparation."));
        var calculator=new CountingCalculator();
        await using var executor=new WorkflowDecisionExecutor();
        var runtime=new WorkflowCalculationRuntime(executor,portfolioCalculator:calculator,preparation:preparation);
        var now=DateTime.UtcNow;
        var view=new Shared.Strategy.Workflow.IntrinsicTime.Model.IntrinsicTimeStrategyWorkflowView
        {WorkflowId=new(Guid.NewGuid()),WorkflowRevision=1,DecisionRuntimeVersion=1,CurrentStage=Shared.Strategy.Workflow.IntrinsicTime.Model.StrategyWorkflowStage.RiskManagement,
            UpdatedAtUtc=now,ExpiresAtUtc=now.AddSeconds(2),RiskManagement=new(){StartedAtUtc=now}};
        var financial=new TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.PortfolioDecisionFinancialState(new(),1,[],now,now.AddSeconds(2),1);
        var failure=await Assert.ThrowsAsync<InvalidOperationException>(()=>runtime.PortfolioAsync(new(),financial,view).AsTask());
        Assert.Equal("WF.DECISION.RUNTIME_NOT_PREPARED",failure.Message);Assert.Equal(0,calculator.Calls);
        Assert.Null(preparation.Failure);Assert.False(preparation.IsReady);
    }
    sealed class CountingCalculator:TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.IPortfolioEntryDecisionCalculator
    {
        public int Calls;
        public TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.PortfolioFundEntryAssessment[] Calculate(
            TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.EvaluatePortfolioOrderCompositionCommand request,
            TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.PortfolioDecisionFinancialState state,CancellationToken token)
        {Calls++;return [];}
    }
}
