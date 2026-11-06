using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.BDDTests;

public sealed class ExitWorkflowLifecycleScenarios
{
    static readonly DateTime Now = new(2026, 9, 10, 17, 30, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(TradeStrategyKind.FuturesOutright)]
    [InlineData(TradeStrategyKind.VerticalSpread)]
    [InlineData(TradeStrategyKind.IronCondor)]
    public void Given_an_accepted_close_when_its_full_checkpoint_is_reloaded_then_a_retry_preserves_the_single_binding(TradeStrategyKind kind)
    {
        switch (kind)
        {
            case TradeStrategyKind.FuturesOutright: Verify<FuturesExitPositionWorkflowCommandState>(kind); break;
            case TradeStrategyKind.VerticalSpread: Verify<VerticalSpreadExitPositionWorkflowCommandState>(kind); break;
            case TradeStrategyKind.IronCondor: Verify<IronCondorExitPositionWorkflowCommandState>(kind); break;
        }
    }

    [Fact]
    public void Given_a_terminal_no_order_decision_when_composition_is_retried_then_no_new_work_is_created()
    {
        var started = Start(TradeStrategyKind.FuturesOutright);
        var state = new FuturesExitPositionWorkflowCommandState();
        state.ReplayEvents([started]);
        var composition = Composition(started);
        composition.Execute(state).Success.Should().BeTrue();
        var source = state.Events.Single();
        Risk(started, composition, source, new() { ExecuteTradeOrder = false, ReasonCode = "No qualified close" })
            .Execute(state).Success.Should().BeTrue();
        state.Snapshot!.Projection.State.Should().Be(ExitPositionWorkflowState.NoTradeOrders);
        state.Snapshot.Projection.StageRevision.Should().Be(3);
        state.AcceptChanges();

        composition.Execute(state).Success.Should().BeTrue();
        state.Events.Should().BeEmpty();
    }

    static void Verify<TState>(TradeStrategyKind kind) where TState : ExitPositionWorkflowCommandStateBase<TState>, new()
    {
        var started = Start(kind);
        var state = new TState();
        state.ReplayEvents([started]);
        var composition = Composition(started);
        composition.Execute(state).Success.Should().BeTrue();
        var composed = state.Events.Single();
        var decision = new PortfolioCloseRiskDecision
        {
            PortfolioCompletedEventId = Guid.NewGuid(), ExecuteTradeOrder = true,
            TradeOrder = new() { Id = new(1, 2, 80) }, FinancialRevision = 2
        };
        Risk(started, composition, composed, decision).Execute(state).Success.Should().BeTrue();
        var accepted = state.Events.OfType<ExitPositionWorkflowRiskRecordedEvent>().Single();
        var checkpoint = MessagePackSerializer.Deserialize<ExitPositionWorkflowRiskRecordedEvent>(MessagePackSerializer.Serialize(accepted));
        var restored = new TState();
        restored.ReplayEvents([checkpoint]);
        var complete = new CompleteExitPositionWorkflowCommand
        {
            CommandId = Guid.NewGuid(), EntityId = started.EntityId, ExpectedRevision = 3, PredecessorEventId = accepted.Id,
            Subject = Subject(started, CompleteExitPositionWorkflowCommand.Verb),
            ExecutionAttemptId = ExitWorkflowLifecycleIdentity.OrderHandoff(decision, "execution"),
            BindingCommandId = ExitWorkflowLifecycleIdentity.OrderHandoff(decision, "bind")
        };
        complete.Execute(restored).Success.Should().BeTrue();
        restored.Snapshot!.Projection.State.Should().Be(ExitPositionWorkflowState.Completed);
        restored.Snapshot.Projection.StageRevision.Should().Be(4);
        restored.Snapshot.CompositionReceiptId.Should().Be(composition.Receipt.Id);
        restored.Events.Should().ContainSingle().Which.Should().BeOfType<ExitPositionWorkflowCompletedEvent>();
        restored.AcceptChanges();
        complete.Execute(restored).Success.Should().BeTrue();
        restored.Events.Should().BeEmpty();
    }

    static ExitPositionWorkflowStartedEvent Start(TradeStrategyKind kind)
    {
        var date = DateOnly.FromDateTime(Now);
        var position = new StrategyPositionSnapshot { Id = new(new TradeEntityId(1, 2, 3, 4), Guid.NewGuid()), StrategyKind = kind };
        return new()
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = new(position.Id, date, Guid.NewGuid()),
            LifecycleVersion = 1, StrategyKind = kind, SourcePlanEventId = Guid.NewGuid(), ReceivedOn = Now,
            ExitPlan = new() { Position = position, ValueDate = date, ContentHash = "exit-plan", State = TradePlanState.ExitRequired,
                Action = TradePlanAction.ExitAtMarket, RequiresExit = true }
        };
    }

    static RecordExitOrderCompositionCommand Composition(ExitPositionWorkflowStartedEvent started) => new()
    {
        CommandId = Guid.NewGuid(), Subject = Subject(started, RecordExitOrderCompositionCommand.Verb), EntityId = started.EntityId,
        ExpectedRevision = 1, PredecessorEventId = started.Id,
        Receipt = new()
        {
            Id = Guid.NewGuid(), CommandId = ExitWorkflowLifecycleIdentity.Function(started.EntityId, ComposeExitOrderCommand.Verb),
            EntityId = started.EntityId, ReceivedOn = Now.AddSeconds(1),
            RequestFingerprint = ExitWorkflowLifecycleIdentity.CompositionFingerprint(started),
            Composition = new() { WorkflowId = started.EntityId, StrategyKind = started.StrategyKind, Position = started.ExitPlan.Position,
                ExitAction = started.ExitPlan.Action, CompositionHash = "composition" }
        }
    };

    static RecordExitRiskDecisionCommand Risk(ExitPositionWorkflowStartedEvent started, RecordExitOrderCompositionCommand composed,
        IEvent source, PortfolioCloseRiskDecision decision) => new()
    {
        CommandId = Guid.NewGuid(), Subject = Subject(started, RecordExitRiskDecisionCommand.Verb), EntityId = started.EntityId,
        ExpectedRevision = 2, PredecessorEventId = source.Id,
        Receipt = new()
        {
            Id = Guid.NewGuid(), CommandId = ExitWorkflowLifecycleIdentity.Function(started.EntityId, EvaluatePositionExitRiskCommand.Verb),
            EntityId = started.EntityId, ReceivedOn = Now.AddSeconds(2), Decision = decision,
            RequestFingerprint = ExitWorkflowLifecycleIdentity.RiskFingerprint(composed.Receipt.Composition, composed.Receipt.Id)
        }
    };

    static ActorSubject Subject(ExitPositionWorkflowStartedEvent started, string verb) =>
        new(ActorType.Command, ExitPositionWorkflowActorNames.Owner(started.StrategyKind), verb, started.EntityId.Format());
}
