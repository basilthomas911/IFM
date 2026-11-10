using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Application.Serialization;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Portfolio;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;

namespace TomasAI.IFM.Domain.Trade.BDDTests;

/// <summary>Admission scenarios use explicit synthetic composition and Portfolio evidence; broker fills are integration acceptance.</summary>
public sealed class GivenAuthorizedWorkflowWhenOpeningIsAdmittedTests
{
    [Theory]
    [InlineData(TimeFrameType.Monthly, "ShortBalancedIronCondor", 4)]
    [InlineData(TimeFrameType.Weekly, "BullCallDebit", 2)]
    [InlineData(TimeFrameType.Daily, "LongFuture", 1)]
    public async Task Then_restart_and_repeated_delivery_preserve_one_exact_opening_intent(TimeFrameType horizon, string variant, int legs)
    {
        // Given a composed order and a fixture Portfolio approval under the frozen emulator route.
        var source = await Authorized(horizon, variant);
        var now = source.UpdatedAtUtc;
        var start = StrategyOpeningIntentFactory.Create(source, now).Single();
        var state = new StrategyExecutionHandoffCommandState();
        start.Execute(state, now).Success.Should().BeTrue();
        state.Definition!.Order.Components.Single().Legs.Should().HaveCount(legs);

        // When the committed intent is restored after a lost acknowledgement and the source is delivered again.
        var committed = new StrategyExecutionHandoffStateSnapshottedEvent { EntityId = start.EntityId, Definition = state.Definition };
        var bytes = MessagePackBinarySerializer.Shared.Serialize(committed);
        var restored = new StrategyExecutionHandoffCommandState();
        restored.Apply(MessagePackBinarySerializer.Shared.Deserialize<StrategyExecutionHandoffStateSnapshottedEvent>(bytes)!, addEvent: false).Should().BeTrue();
        var duplicate = StrategyOpeningIntentFactory.Create(source, now.AddSeconds(1)).Single();
        duplicate.Execute(restored, now.AddSeconds(1)).Success.Should().BeTrue();

        // Then the original execution, order, leg identities and price remain unchanged; admission alone is not an opening.
        duplicate.CommandId.Should().Be(start.CommandId);
        restored.Definition!.Id.Should().Be(state.Definition.Id);
        restored.Definition.IntentHash.Should().Be(state.Definition.IntentHash);
        restored.Definition.Revision.Should().Be(1);
        restored.Definition.Phase.Should().Be(StrategyExecutionHandoffPhase.Accepted);
        restored.Definition.Execution.Should().BeNull();
        FinancialCanonicalHash.Compute(restored.Definition.Order).Should().Be(FinancialCanonicalHash.Compute(state.Definition.Order));
    }

    [Fact]
    public async Task Given_no_trade_or_a_historical_workflow_then_no_opening_is_admitted()
    {
        var source = await Authorized(TimeFrameType.Daily, "LongFuture");
        StrategyOpeningIntentFactory.Create(source with { State = source.State with { Outcome = StrategyWorkflowOutcome.NoTrade } }, source.UpdatedAtUtc).Should().BeEmpty();
        StrategyOpeningIntentFactory.Create(source with { State = source.State with { OpeningExecutionRoute = null } }, source.UpdatedAtUtc).Should().BeEmpty();
    }

    static async Task<WorkflowStrategyStateUpdatedEvent> Authorized(TimeFrameType horizon, string variant)
    {
        var input = await CompositionFixture.Command(variant, horizon);
        var composition = new OrderComposer(new Black76ComposerPricer()).Calculate(input);
        composition.Candidate.Should().NotBeNull();
        composition = composition with { Candidate = composition.Candidate! with { PortfolioId = 0, FundId = 0, OrderId = 0, PrimaryTradeId = 0 } };
        var route = new StrategyOpeningExecutionRoute { AccountAlias = "IFM-EMULATOR-PAPER", Environment = BrokerEnvironment.Emulator, PolicyHash = new('A', 64) };
        var view = input.WorkflowView with
        {
            WorkflowRevision = 7, CurrentStage = StrategyWorkflowStage.RiskManagement,
            Status = WorkflowStrategyMachineStatus.Completed, Outcome = StrategyWorkflowOutcome.Completed,
            OpeningExecutionRoute = route, CompositionExecution = input,
            SelectionBinding = input.SelectionBinding with { SchemaVersion = 2 },
            OrderComposition = input.WorkflowView.OrderComposition with { Result = StrategyStageResultEnvelope.CreateComposition(composition) }
        };
        var prepared = PortfolioOrderCompositionMapper.CreateRequest(view, 7, input.EvaluatedAtUtc);
        var component = prepared.Body.Components.Single().ToTradeComponent() with { ReservedTradeId = 801 };
        var order = new TradeOrderDefinition
        {
            Id = new(7, 8, 701), Revision = 1, Status = TradeOrderStatus.Approved, ValueDate = prepared.Body.ValueDate,
            ValidUntilUtc = prepared.Body.ValidUntilUtc, PositionType = TradeOrderPositionType.Opening,
            BrokerAccountAlias = route.AccountAlias, BrokerEnvironment = route.Environment, PortfolioApprovalId = Guid.NewGuid(),
            DefinitionHash = prepared.Body.EvidenceHash, MicroExecutionProfileId = route.PolicyId,
            MicroExecutionProfileVersion = 1, MicroExecutionProfileHash = route.PolicyHash, Components = [component]
        };
        view = view with { PortfolioRiskDecision = new() { PortfolioId = 7, WorkflowId = view.WorkflowId.Value,
            Status = PortfolioRiskDecisionStatus.ExecuteTradeOrders, TradeOrders = [order], FinancialRevision = 1 } };
        return new() { Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EventId = 1, EntityId = view.EntityId,
            WorkflowId = view.WorkflowId, WorkflowRevision = view.WorkflowRevision, State = view, UpdatedAtUtc = input.EvaluatedAtUtc };
    }
}
