using System.Collections.Immutable;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
using Composer = TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;

public sealed class CompositionAcceptanceTests
{
    [Theory]
    [InlineData("price")] [InlineData("quantity")] [InlineData("side")] [InlineData("reservation")]
    [InlineData("contracts")] [InlineData("invocation")] [InlineData("expiry")]
    public async Task Altered_or_expired_completions_never_start_risk(string alteration)
    {
        var c = await CompositionFixture.Command(); var model = new Composer(new Black76ComposerPricer());
        var result = model.Calculate(c); var candidate = result.Candidate!;
        if (alteration == "price") candidate = candidate with { Pricing = candidate.Pricing with { LimitDebit = candidate.Pricing.LimitDebit + 1 } };
        if (alteration == "quantity") candidate = candidate with { UnitQuantity = 2 };
        if (alteration == "side") candidate = candidate with { Side = "Short" };
        if (alteration == "reservation") candidate = candidate with { OrderId = 9000 };
        candidate = candidate with { CandidateHash = CompositionHash.Candidate(candidate) };
        var command = CompositionFixture.Completion(c, result with { Candidate = candidate });
        if (alteration == "contracts") command = command with { SelectedContracts = new(c.MarketSnapshot.ScopeId, ["OTHER"]) };
        if (alteration == "invocation") command = command with { SourceEventId = Guid.NewGuid() };
        var fixture = new TradeSelectionHandoffTests.Fixture(c.WorkflowView.SelectionDispatch!);
        fixture.Restore(c.WorkflowView with { CompositionExecution = c });
        if (alteration == "expiry") fixture.Clock.Now = c.ExpiresAtUtc;
        command.Execute(fixture.Context, fixture.State);
        Assert.NotEqual(StrategyWorkflowStage.RiskManagement, fixture.State.CurrentView!.CurrentStage);
        Assert.NotEqual(WorkflowStrategyMachineStatus.Started, fixture.State.CurrentView.Status);
    }
    [Fact]
    public async Task Accepted_candidate_advances_once_and_preserves_the_immutable_risk_input()
    {
        var c = await CompositionFixture.Command("LongBullishIronCondor");
        var result = new Composer(new Black76ComposerPricer()).Calculate(c);
        var fixture = new TradeSelectionHandoffTests.Fixture(c.WorkflowView.SelectionDispatch!);
        fixture.Restore(c.WorkflowView with { CompositionExecution = c });
        var command = CompositionFixture.Completion(c, result); command.Execute(fixture.Context, fixture.State);
        var view = fixture.State.CurrentView!;
        Assert.Equal(StrategyWorkflowStage.RiskManagement, view.CurrentStage);
        Assert.Equal(c.InputWorkflowRevision + 1, view.WorkflowRevision);
        Assert.Equal(result.Candidate!.CandidateHash, view.OrderComposition.Result!.ReadCompositionResult().Candidate!.CandidateHash);
        command.Execute(fixture.Context, fixture.State);
        Assert.Equal(CompositionHash.Compute(view), CompositionHash.Compute(fixture.State.CurrentView));
    }
    [Fact]
    public async Task NoCandidate_is_a_normal_business_stop_and_legacy_success_cannot_bypass_validation()
    {
        var c = await CompositionFixture.Command();
        var snapshot = c.MarketSnapshot with { Instruments = [], Digest = "" };
        snapshot = snapshot with { Digest = CompositionSemanticHash.Compute(snapshot) };
        c = CompositionFixture.Seal(c with { MarketSnapshot = snapshot });
        var r = new Composer(new Black76ComposerPricer()).Calculate(c);
        var fixture = new TradeSelectionHandoffTests.Fixture(c.WorkflowView.SelectionDispatch!);
        fixture.Restore(c.WorkflowView with { CompositionExecution = c });
        CompositionFixture.Completion(c, r).Execute(fixture.Context, fixture.State);
        Assert.Equal(WorkflowStrategyMachineStatus.Completed, fixture.State.CurrentView!.Status);
        Assert.Equal(StrategyWorkflowOutcome.NoTrade, fixture.State.CurrentView.Outcome);
        Assert.Equal(StrategyWorkflowContinuationDecision.Stop, fixture.State.CurrentView.OrderComposition.ContinuationDecision);
        fixture.Restore(c.WorkflowView with { CompositionExecution = null });
        CompositionFixture.Completion(c, r).Execute(fixture.Context, fixture.State);
        Assert.Equal(WorkflowStrategyMachineStatus.Failed, fixture.State.CurrentView!.Status);
    }
}
