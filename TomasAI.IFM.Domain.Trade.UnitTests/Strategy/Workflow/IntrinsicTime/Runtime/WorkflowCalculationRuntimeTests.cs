using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.Runtime;

[Collection(WorkflowDecisionQualificationCollection.Name)]
public sealed class WorkflowCalculationRuntimeTests
{
    [Theory]
    [InlineData("LongFuture")]
    [InlineData("BearPutDebit")]
    [InlineData("ShortBalancedIronCondor")]
    public async Task Local_selection_requires_published_inputs_and_preserves_the_original_decision(string variant)
    {
        var command = await TradeSelection.TradeSelectionFixture.Command(variant);
        var clock = new FixedClock(command.EvaluatedAtUtc);
        var cache = new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.WorkflowSelectionConfigurationCache(clock);
        await using var executor = new WorkflowDecisionExecutor(timeProvider: clock);
        var runtime = new WorkflowCalculationRuntime(executor, clock, selectionConfiguration: cache);
        Func<Task> missing = async () => await runtime.SelectionAsync(command);
        await missing.Should().ThrowAsync<InvalidOperationException>().WithMessage("TS.CONFIG.COMPILED_NOT_READY");
        // A later signal can use a newly published generation; the unavailable signal is terminal at the manager boundary.
        command = await TradeSelection.TradeSelectionFixture.Command(variant);
        cache.PublishCompiled(command.SelectionBinding);
        var before = TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model.TradeSelectionEvaluator.Evaluate(command);
        var after = await runtime.SelectionAsync(command);
        after.Should().BeEquivalentTo(before);
        (await runtime.SelectionAsync(command)).Should().BeSameAs(after);
    }

    [Fact]
    public async Task Duplicate_notifications_share_one_calculation_and_a_changed_revision_cannot_rerun_it()
    {
        await using var executor = new WorkflowDecisionExecutor();
        var calculator = new CountingCalculator();
        var runtime = new WorkflowCalculationRuntime(executor, portfolioCalculator: calculator);
        var now = DateTime.UtcNow;
        var view = View(now);
        var request = new EvaluatePortfolioOrderCompositionCommand { ExpiresAtUtc = now.AddSeconds(10) };
        var financial = Financial(now);
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => runtime.PortfolioAsync(request, financial, view).AsTask()));
        calculator.Calls.Should().Be(1);
        results.Should().OnlyContain(value => ReferenceEquals(value, results[0]));
        results[0].HasValidFingerprint().Should().BeTrue();
        runtime.MatchesApproval(view, results[0]).Should().BeTrue();
        runtime.MatchesApproval(view, results[0] with { }).Should().BeFalse("only the owner-produced approval can cross the local decision boundary");
        runtime.MatchesApproval(view, results[0] with { PayloadSha256 = new('A', 64) }).Should().BeFalse();
        Func<Task> changed = async () => await runtime.PortfolioAsync(request, financial, view with { WorkflowRevision = 2 });
        await changed.Should().ThrowAsync<InvalidOperationException>().WithMessage("WF.DECISION.INPUT_REVISION_CHANGED");
        calculator.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Expired_stage_is_terminal_for_this_attempt_and_has_no_financial_effects()
    {
        await using var executor = new WorkflowDecisionExecutor();
        var calculator = new CountingCalculator();
        var runtime = new WorkflowCalculationRuntime(executor, portfolioCalculator: calculator);
        var now = DateTime.UtcNow;
        var view = View(now.AddSeconds(-3));
        var request = new EvaluatePortfolioOrderCompositionCommand { ExpiresAtUtc = now.AddSeconds(10) };
        for (var notification = 0; notification < 2; notification++)
        {
            Func<Task> attempt = async () => await runtime.PortfolioAsync(request, Financial(now), view);
            (await attempt.Should().ThrowAsync<WorkflowDecisionException>()).Which.Status.Should().Be(DecisionExecutionStatus.DeadlineExceeded);
        }
        calculator.Calls.Should().Be(0);
    }

    static IntrinsicTimeStrategyWorkflowView View(DateTime ready) => new()
    {
        WorkflowId = new(Guid.NewGuid()), WorkflowRevision = 1, DecisionRuntimeVersion = 1,
        CurrentStage = StrategyWorkflowStage.RiskManagement, Status = WorkflowStrategyMachineStatus.Started,
        UpdatedAtUtc = ready, ExpiresAtUtc = ready.AddSeconds(10),
        RiskManagement = new() { StartedAtUtc = ready, ProcessingStatus = StrategyActorProcessingStatus.Processing }
    };
    static PortfolioDecisionFinancialState Financial(DateTime now) => new(new FinancialBookConfiguration(), 1, [], now, now.AddSeconds(3), 1);
    sealed class CountingCalculator : IPortfolioEntryDecisionCalculator
    {
        public int Calls;
        public PortfolioFundEntryAssessment[] Calculate(EvaluatePortfolioOrderCompositionCommand request, PortfolioDecisionFinancialState state, CancellationToken cancellationToken)
        { Interlocked.Increment(ref Calls); cancellationToken.ThrowIfCancellationRequested(); return []; }
    }
    sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
