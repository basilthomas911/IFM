using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using static TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection.TradeSelectionTestInputs;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;

/// <summary>Separates bounded processing from the original permission to create exposure.</summary>
public sealed class TradeSelectionDeadlineTests
{
    [Fact]
    public async Task Dispatch_uses_execution_budget_without_extending_market_permission()
    {
        var command = await TradeSelectionFixture.Command();
        command = Evidence(command, assessmentChange: assessment => Set(assessment, "ValidUntilUtc", command.EvaluatedAtUtc.AddMilliseconds(100)));
        var dispatch = TradeSelectionDispatch.Create(command.WorkflowView, Guid.NewGuid());
        dispatch.ExpiresAtUtc.Should().Be(dispatch.RequestedAtUtc.AddMilliseconds(TradeSelectionContracts.CommonPolicy(dispatch.SelectionBinding).MaximumExecutionMilliseconds));
        var fixture = new TradeSelectionHandoffTests.Fixture(dispatch);
        fixture.Result.ValidUntilUtc.Should().Be(command.EvaluatedAtUtc.AddMilliseconds(100));
        fixture.Clock.Now = fixture.Result.ValidUntilUtc;
        fixture.Accept();
        fixture.State.CurrentView!.TradeSelection.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.TimedOut);
        fixture.State.CurrentView.CompositionHandoff.Should().BeNull();
    }

    [Fact]
    public async Task Evaluation_at_evidence_expiry_is_rejected_even_with_processing_time_left()
    {
        var command = await TradeSelectionFixture.Command();
        command = Evidence(command, assessmentChange: assessment => Set(assessment, "ValidUntilUtc", command.EvaluatedAtUtc));
        Action validate = () => TradeSelectionContracts.ValidateRequest(command);
        validate.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Refusal_can_complete_after_evidence_expiry_without_starting_composition()
    {
        var command = await TradeSelectionFixture.Command();
        command = Authority(command, command.SelectionBinding.PortfolioSnapshot with
        { Fund = command.SelectionBinding.PortfolioSnapshot.Fund with { PermittedTradeStrategyFamilies = [] } });
        command = Evidence(command, assessmentChange: assessment => Set(assessment, "ValidUntilUtc", command.EvaluatedAtUtc.AddMilliseconds(100)));
        var dispatch = TradeSelectionDispatch.Create(command.WorkflowView, Guid.NewGuid());
        var function = new TradeSelectionFunctionTests.FunctionFixture(dispatch);
        function.Clock.Now = dispatch.EvaluatedAtUtc.AddMilliseconds(500);
        var terminal = await function.Execute();
        terminal.IsCompleted.Should().BeTrue(terminal.Failed?.ReasonCode);
        var result = TradeSelectionContracts.ReadResult(terminal.Completed!.Result);
        result.Outcome.Should().Be(SelectionOutcome.NoTrade);
        result.PrimaryReasonCode.Should().Be("TS.NO_AUTHORIZED_CANDIDATE");
        result.ValidUntilUtc.Should().Be(dispatch.EvaluatedAtUtc.AddMilliseconds(100));
        var workflow = new TradeSelectionHandoffTests.Fixture(dispatch);
        workflow.Clock.Now = function.Clock.Now;
        workflow.Accept(result);
        workflow.State.CurrentView!.Outcome.Should().Be(StrategyWorkflowOutcome.NoTrade);
        workflow.State.CurrentView.StopReasonCode.Should().Be(result.PrimaryReasonCode);
        workflow.State.CurrentView.CompositionHandoff.Should().BeNull();
    }
}
