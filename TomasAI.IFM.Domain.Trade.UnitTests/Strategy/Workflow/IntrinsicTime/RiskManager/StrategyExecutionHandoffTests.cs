using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.State;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;

/// <summary>Opening acceptance must preserve financial authority and original identities across replay.</summary>
public sealed class StrategyExecutionHandoffTests
{
    static readonly DateTime Now = new(2026, 11, 9, 23, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Concurrent_ready_results_freeze_one_binding_time_at_acceptance()
    {
        var start = Intent(); var state = new StrategyExecutionHandoffCommandState();
        start.Execute(state, Now).Success.Should().BeTrue();
        var approved = state.Definition! with { Revision = 4, Phase = StrategyExecutionHandoffPhase.OrderApproved };
        state.Apply(new StrategyExecutionHandoffStateSnapshottedEvent { EntityId = start.EntityId, Definition = approved }, addEvent: false).Should().BeTrue();
        var ready = new AdvanceStrategyExecutionHandoffCommand { CommandId = Guid.NewGuid(), EntityId = start.EntityId,
            ExpectedRevision = 4, Evidence = approved with { Phase = StrategyExecutionHandoffPhase.OrderReady } };
        ready.Execute(state, Now.AddSeconds(1)).Success.Should().BeTrue();
        (ready with { CommandId = Guid.NewGuid() }).Execute(state, Now.AddSeconds(2)).Success.Should().BeTrue();
        state.Definition!.Revision.Should().Be(5);
        state.Definition.ExecutionRequestedAtUtc.Should().Be(Now.AddSeconds(1));
    }

    [Fact]
    public void A_confirmed_late_fill_can_replace_a_cancelled_summary_but_cannot_be_reported_as_rejected()
    {
        var command = Intent(); var state = new StrategyExecutionHandoffCommandState();
        command.Execute(state, Now).Success.Should().BeTrue();
        var cancelled = new OrderExecutionDefinition { TradeOrderId = command.Intent.Order.Id,
            ExecutionAttemptId = command.EntityId.ExecutionAttemptId, Order = command.Intent.Order,
            Status = OrderExecutionStatus.Cancelled, OrderQuantity = 1 };
        var checkpoint = state.Definition! with { Revision = 2, Phase = StrategyExecutionHandoffPhase.Cancelled,
            Execution = cancelled, FinancialReceipt = Receipt(cancelled), FundEvidence = new() { Status = "Cancelled" } };
        state.Apply(new StrategyExecutionHandoffStateSnapshottedEvent { EntityId = command.EntityId, Definition = checkpoint }, addEvent: false).Should().BeTrue();
        var filled = cancelled with { Status = OrderExecutionStatus.Filled, CumulativeFilledQuantity = 1,
            Fills = [new() { ExecutionFillId = Guid.NewGuid(), SignedQuantity = 1 }] };
        var evidence = checkpoint with { Phase = StrategyExecutionHandoffPhase.Opened, Execution = filled,
            FinancialReceipt = Receipt(filled), FundEvidence = new() { Status = "Opened" } };
        var update = new AdvanceStrategyExecutionHandoffCommand { EntityId = command.EntityId, CommandId = Guid.NewGuid(),
            ExpectedRevision = 2, Evidence = evidence };
        (update with { Evidence = evidence with { Phase = StrategyExecutionHandoffPhase.Rejected } }).Execute(state, Now).Success.Should().BeFalse();
        update.Execute(state, Now).Success.Should().BeTrue();
        state.Definition!.Phase.Should().Be(StrategyExecutionHandoffPhase.Opened);
        state.Definition.Execution!.CumulativeFilledQuantity.Should().Be(1);

        static LedgerPostingBatchCompletedEvent Receipt(OrderExecutionDefinition execution) => new()
        { ProjectionFacts = new() { ExecutionCapacity = new() { Outcome = new()
            { ExecutionId = execution.ExecutionAttemptId, ExecutionHash = FinancialCanonicalHash.Compute(execution) } } } };
    }

    [Fact]
    public void Binding_timestamp_survives_snapshot_and_command_transport_without_changing_the_request()
    {
        var command = Intent(); var state = new StrategyExecutionHandoffCommandState();
        command.Execute(state, Now).Success.Should().BeTrue();
        var at = Now.AddTicks(1234567);
        var source = new StrategyExecutionHandoffStateSnapshottedEvent { EntityId = command.EntityId,
            Definition = state.Definition! with { Phase = StrategyExecutionHandoffPhase.OrderReady, ExecutionRequestedAtUtc = at } };
        var codec = TomasAI.IFM.Application.Serialization.MessagePackBinarySerializer.Shared;
        var restored = new StrategyExecutionHandoffCommandState();
        restored.Apply(codec.Deserialize<StrategyExecutionHandoffStateSnapshottedEvent>(codec.Serialize(source)!)!, addEvent: false).Should().BeTrue();
        var advance = new AdvanceStrategyExecutionHandoffCommand { CommandId = Guid.NewGuid(), EntityId = command.EntityId,
            ExpectedRevision = restored.Definition!.Revision, Evidence = source.Definition with { Phase = StrategyExecutionHandoffPhase.ExecutionLinked } };
        var wire = codec.Deserialize<AdvanceStrategyExecutionHandoffCommand>(codec.Serialize(advance)!)!;
        wire.Execute(restored, Now.AddSeconds(1)).Success.Should().BeTrue();
        restored.Definition!.ExecutionRequestedAtUtc.Should().Be(at);
    }

    [Fact]
    public void Handoff_audit_payload_does_not_copy_workflow_market_data()
    {
        var command = Intent();
        var codec = new TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditMessagePackCodec();
        codec.Serialize(command).Bytes.Length.Should().BeLessThan(16_384);
        command.Intent.Authorization.StateHash.Should().HaveLength(64);
    }

    [Fact]
    public void Repeated_authorization_keeps_one_intent_and_rejects_modified_content_under_the_same_hash()
    {
        var command = Intent();
        var state = new StrategyExecutionHandoffCommandState();
        command.Execute(state, Now).Success.Should().BeTrue();
        var snapshot = FinancialCanonicalHash.Compute(state.Definition);
        command.Execute(state, Now.AddSeconds(1)).Success.Should().BeTrue();
        FinancialCanonicalHash.Compute(state.Definition).Should().Be(snapshot);
        var forged = command with { Intent = command.Intent with { Order = command.Intent.Order with { BrokerAccountAlias = "another-account" } } };
        forged.Execute(state, Now).Success.Should().BeFalse();
        FinancialCanonicalHash.Compute(state.Definition).Should().Be(snapshot);
    }

    [Fact]
    public void Historical_or_no_trade_workflow_cannot_create_an_opening_intent()
    {
        var source = Source();
        StrategyOpeningIntentFactory.Create(source with { State = source.State with { OpeningExecutionRoute = null } }, Now).Should().BeEmpty();
        StrategyOpeningIntentFactory.Create(source with { State = source.State with { Outcome = StrategyWorkflowOutcome.NoTrade } }, Now).Should().BeEmpty();
    }

    [Fact]
    public void Expired_intent_is_retained_for_release_but_a_foreign_environment_cannot_begin_execution()
    {
        var command = Intent();
        var expired = new StrategyExecutionHandoffCommandState();
        command.Execute(expired, Now.AddMinutes(2)).Success.Should().BeTrue();
        expired.Definition!.Phase.Should().Be(StrategyExecutionHandoffPhase.Accepted);
        expired.Definition.Execution.Should().BeNull();
        var foreign = command with { Intent = command.Intent with { Order = command.Intent.Order with { BrokerEnvironment = BrokerEnvironment.Live } } };
        foreign.Execute(new(), Now).Success.Should().BeFalse();
    }

    [Fact]
    public void Latest_complete_snapshot_restores_pending_request_and_duplicate_fence()
    {
        var command = Intent();
        var original = new StrategyExecutionHandoffCommandState();
        command.Execute(original, Now).Success.Should().BeTrue();
        var advance = new AdvanceStrategyExecutionHandoffCommand { CommandId = Guid.NewGuid(), EntityId = command.EntityId,
            ExpectedRevision = 1, Evidence = original.Definition! with { Phase = StrategyExecutionHandoffPhase.FinancialAdmitted } };
        advance.Execute(original, Now.AddSeconds(1)).Success.Should().BeTrue();
        var latest = new StrategyExecutionHandoffStateSnapshottedEvent { EntityId = command.EntityId, Definition = original.Definition! };
        var restored = new StrategyExecutionHandoffCommandState();
        restored.Apply(latest, addEvent: false).Should().BeTrue();
        restored.Definition.Should().Be(original.Definition);
        command.Execute(restored, Now.AddSeconds(2)).Success.Should().BeTrue();
        restored.Definition!.Revision.Should().Be(2);
    }

    [Fact]
    public void Stale_prepared_evidence_does_not_regress_a_newer_checkpoint()
    {
        var command = Intent(); var state = new StrategyExecutionHandoffCommandState();
        command.Execute(state, Now).Success.Should().BeTrue();
        var advance = new AdvanceStrategyExecutionHandoffCommand { CommandId = Guid.NewGuid(), EntityId = command.EntityId,
            ExpectedRevision = 1, Evidence = state.Definition! with { Phase = StrategyExecutionHandoffPhase.FinancialAdmitted } };
        advance.Execute(state, Now.AddSeconds(1)).Success.Should().BeTrue();
        advance.Execute(state, Now.AddSeconds(2)).Success.Should().BeTrue();
        state.Definition!.Revision.Should().Be(2);
    }

    static BeginStrategyExecutionHandoffCommand Intent()
        => StrategyOpeningIntentFactory.Create(Source(), Now).Single();

    static WorkflowStrategyStateUpdatedEvent Source()
    {
        var route = new StrategyOpeningExecutionRoute { AccountAlias = "IFM-EMULATOR-PAPER", Environment = BrokerEnvironment.Emulator, PolicyHash = new('A', 64) };
        var order = new TradeOrderDefinition
        {
            Id = new(1, 2, 3), Revision = 1, Status = TradeOrderStatus.Approved,
            ValueDate = new(2026, 11, 10), ValidUntilUtc = Now.AddMinutes(1), PortfolioApprovalId = Guid.NewGuid(),
            PositionType = TradeOrderPositionType.Opening, BrokerAccountAlias = route.AccountAlias, BrokerEnvironment = route.Environment,
            DefinitionHash = new('B', 64), MicroExecutionProfileHash = route.PolicyHash,
            Components = [new() { ComponentId = Guid.NewGuid(), ReservedTradeId = 4, StrategyKind = TradeStrategyKind.FuturesOutright,
                SignedNetDebitLimit = 7580, MinimumSignedNetDebitLimit = 7580, MaximumSignedNetDebitLimit = 7581, TickIncrement = .25m,
                Legs = [new() { TradeLegId = Guid.NewGuid(), ContractId = "ES20261218", ContractKey = "ES20261218",
                    AssetFamily = TradeAssetFamily.Futures, SignedQuantity = 1, CashMultiplier = 50 }] }]
        };
        var workflowId = Guid.NewGuid();
        var workflow = new IntrinsicTimeStrategyWorkflowView { WorkflowId = new(workflowId), WorkflowRevision = 7,
            TriggerEvent = new() { EntityId = new() { ContractId = "ES20261218", ValueDate = new(2026, 11, 10),
                TimePeriod = TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily } },
            Status = WorkflowStrategyMachineStatus.Completed, Outcome = StrategyWorkflowOutcome.Completed,
            CurrentStage = StrategyWorkflowStage.RiskManagement, OpeningExecutionRoute = route,
            SelectionBinding = new() { SchemaVersion = 2 },
            PortfolioRiskDecision = new() { WorkflowId = workflowId, PortfolioId = 1, Status = PortfolioRiskDecisionStatus.ExecuteTradeOrders, TradeOrders = [order], FinancialRevision = 1 } };
        var source = new WorkflowStrategyStateUpdatedEvent { Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EventId = 1,
            WorkflowId = workflow.WorkflowId, WorkflowRevision = 7, State = workflow, UpdatedAtUtc = Now };
        return source;
    }
}
