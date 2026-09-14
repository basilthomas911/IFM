using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Order.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Model;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using StrategyIronCondorTradePlanId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.TradeFlow;

public sealed class TradeFlowSerializationAndRecoveryTests
{
    [Fact]
    public void Trade_order_messagepack_round_trip_preserves_stable_ownership_and_leg_identity()
    {
        var legId = Guid.NewGuid();
        var order = CreateOrder(legId);

        var payload = MessagePackSerializer.Serialize(order);
        var restored = MessagePackSerializer.Deserialize<TradeOrderDefinition>(payload);

        restored.Should().BeEquivalentTo(order);
        restored.Id.Format().Should().Be("1.2.3");
        restored.Components[0].Legs[0].TradeLegId.Should().Be(legId);
    }

    [Fact]
    public void Replayed_order_state_continues_from_exact_committed_status()
    {
        var original = new TradeOrderActorStateMachine();
        original.Create(CreateOrder(Guid.NewGuid()));
        original.Approve();
        var committed = original.Ready().Value!;
        var payload = MessagePackSerializer.Serialize(committed);

        var recovered = new TradeOrderActorStateMachine();
        recovered.Replay(MessagePackSerializer.Deserialize<TradeOrderDefinition>(payload));

        recovered.Current.Should().BeEquivalentTo(committed);
        recovered.BindExecution(Guid.NewGuid(), ExecutionChannel.Manual, DateTime.UtcNow)
            .Value!.Status.Should().Be(TradeOrderStatus.Executing);
    }

    [Fact]
    public void Closing_order_round_trip_preserves_target_position_and_existing_trade_identity()
    {
        var opening = CreateOrder(Guid.NewGuid());
        var target = new StrategyPositionId(new TradeEntityId(
            opening.Id.PortfolioId, opening.Id.FundId, opening.Id.OrderId,
            opening.Components[0].ReservedTradeId), Guid.NewGuid());
        var close = opening with
        {
            Id = opening.Id with { OrderId = opening.Id.OrderId + 1 },
            PositionType = TradeOrderPositionType.Closing,
            TargetPositionId = target,
            Components = [opening.Components[0] with
            {
                Legs = [opening.Components[0].Legs[0] with { SignedQuantity = -1 }]
            }]
        };

        var restored = MessagePackSerializer.Deserialize<TradeOrderDefinition>(
            MessagePackSerializer.Serialize(close));

        restored.PositionType.Should().Be(TradeOrderPositionType.Closing);
        restored.TargetPositionId.Should().Be(target);
        restored.Components.Single().ReservedTradeId.Should().Be(target.Trade.TradeId);
    }

    [Fact]
    public void Append_only_component_key_preserves_older_payload_compatibility()
    {
#pragma warning disable CS0618 // Compatibility fixture intentionally writes the retired numeric key.
        var legacyPayload = MessagePackSerializer.Serialize(new LegacyComponent(
            Guid.NewGuid(), TradeStrategyKind.FuturesOutright,
            [new TradeLegDefinition { TradeLegId = Guid.NewGuid(), LegacyMarketInstrumentId = 8, AssetFamily = TradeAssetFamily.Futures, SignedQuantity = 1 }],
            false));
#pragma warning restore CS0618

        var restored = MessagePackSerializer.Deserialize<TradeOrderComponentDefinition>(legacyPayload);

        restored.ReservedTradeId.Should().Be(0);
        restored.Legs.Should().ContainSingle();
    }

    [Fact]
    public void Typed_command_and_event_round_trip_and_replay_without_json_fallback()
    {
        var order = CreateOrder(Guid.NewGuid());
        var command = new CreateTradeOrderCommand
        {
            CommandId = Guid.NewGuid(), EntityId = order.Id, Order = order,
            Subject = new ActorSubject(ActorType.Command, TradeOrderActorNames.Command,
                CreateTradeOrderCommand.Verb, order.Id.Format())
        };
        var commandCopy = MessagePackSerializer.Deserialize<CreateTradeOrderCommand>(
            MessagePackSerializer.Serialize(command));
        var changed = new TradeOrderChangedEvent
        {
            EntityId = order.Id, State = order
        };
        var eventCopy = MessagePackSerializer.Deserialize<TradeOrderChangedEvent>(
            MessagePackSerializer.Serialize(changed));
        var state = new TradeOrderCommandState();

        state.ReplayEvents([eventCopy]);

        commandCopy.Should().BeEquivalentTo(command);
        state.Current.Should().BeEquivalentTo(order);
    }

    [Fact]
    public void Strategy_trade_plan_history_query_round_trip_preserves_opaque_paging_state()
    {
        var positionId = new StrategyPositionId(new TradeEntityId(1, 2, 3, 4), Guid.NewGuid());
        var entityId = new StrategyIronCondorTradePlanId(positionId, new DateOnly(2026, 9, 13));
        var query = new GetIronCondorTradePlanHistoryQuery
        {
            PlanId = entityId,
            Subject = new ActorSubject(ActorType.Query, GetIronCondorTradePlanHistoryQuery.Actor,
                GetIronCondorTradePlanHistoryQuery.Verb, entityId.Format()),
            PageSize = 75,
            PagingState = [1, 3, 5, 7]
        };

        var restored = MessagePackSerializer.Deserialize<GetIronCondorTradePlanHistoryQuery>(
            MessagePackSerializer.Serialize(query));

        restored.Should().BeEquivalentTo(query);
        restored.PlanId.Position.Should().Be(positionId);
    }

    [Fact]
    public void Strategy_trade_plan_activity_query_round_trip_preserves_value_date_and_paging_state()
    {
        var query = new GetStrategyTradePlanActivityQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetStrategyTradePlanActivityQuery.Actor,
                GetStrategyTradePlanActivityQuery.Verb, "2026-09-13"),
            ValueDate = new DateOnly(2026, 9, 13),
            PageSize = 125,
            PagingState = [2, 4, 6, 8]
        };

        var restored = MessagePackSerializer.Deserialize<GetStrategyTradePlanActivityQuery>(
            MessagePackSerializer.Serialize(query));

        restored.Should().BeEquivalentTo(query);
    }

    [Fact]
    public void Exit_workflow_query_and_projection_round_trip_preserve_global_position_identity()
    {
        var positionId = new StrategyPositionId(new TradeEntityId(11, 12, 13, 14), Guid.NewGuid());
        var valueDate = new DateOnly(2026, 9, 13);
        var query = new GetPositionExitWorkflowTimelineQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetPositionExitWorkflowQuery.Actor,
                GetPositionExitWorkflowTimelineQuery.Verb, positionId.Format()),
            PositionId = positionId,
            ValueDate = valueDate,
            PageSize = 80,
            PagingState = [9, 7, 5]
        };
        var projection = new ExitPositionWorkflowProjection
        {
            WorkflowId = new ExitPositionWorkflowId(positionId, valueDate, Guid.NewGuid()),
            StrategyKind = TradeStrategyKind.FuturesOutright,
            State = ExitPositionWorkflowState.RiskAccepted,
            StageRevision = 3,
            UpdatedAtUtc = new DateTime(2026, 9, 13, 14, 0, 0, DateTimeKind.Utc),
            SourcePlanEventId = Guid.NewGuid(),
            ExitPlan = new StrategyTradePlanSnapshot
            {
                Position = new StrategyPositionSnapshot
                {
                    Id = positionId,
                    StrategyKind = TradeStrategyKind.FuturesOutright
                },
                ValueDate = valueDate
            },
            RiskDecision = new PortfolioCloseRiskDecision
            {
                ExecuteTradeOrder = true,
                ReasonCode = "Accepted",
                FinancialRevision = 42
            }
        };

        var restoredQuery = MessagePackSerializer.Deserialize<GetPositionExitWorkflowTimelineQuery>(
            MessagePackSerializer.Serialize(query));
        var page = new PositionExitWorkflowHistoryPage([projection], [3, 1, 4]);
        var restoredPage = MessagePackSerializer.Deserialize<PositionExitWorkflowHistoryPage>(
            MessagePackSerializer.Serialize(page));

        restoredQuery.Should().BeEquivalentTo(query);
        restoredPage.Should().BeEquivalentTo(page);
        restoredPage.Items.Single().WorkflowId.Position.Should().Be(positionId);
    }

    [Fact]
    public void Closed_established_trade_round_trip_retains_opening_and_closing_fill_evidence()
    {
        var order = CreateOrder(Guid.NewGuid());
        var openingAttempt = Guid.NewGuid();
        var closingAttempt = Guid.NewGuid();
        var leg = order.Components.Single().Legs.Single();
        var openingFill = Fill(leg, order.Components[0].ComponentId, openingAttempt, leg.SignedQuantity, "OPEN");
        var closingFill = Fill(leg, order.Components[0].ComponentId, closingAttempt, -leg.SignedQuantity, "CLOSE");
        var trade = new EstablishedTradeDefinition
        {
            Id = new(order.Id.PortfolioId, order.Id.FundId, order.Id.OrderId,
                order.Components[0].ReservedTradeId),
            AssetFamily = leg.AssetFamily,
            StrategyKind = order.Components[0].StrategyKind,
            SourceComponentId = order.Components[0].ComponentId,
            ExecutionAttemptId = openingAttempt,
            Status = EstablishedTradeStatus.Closed,
            Legs = [leg],
            OriginalFills = [openingFill],
            ClosingFills = [closingFill],
            EstablishedAtUtc = openingFill.FilledAtUtc,
            ClosedAtUtc = closingFill.FilledAtUtc,
            EvidenceRevision = 2
        };

        var restored = MessagePackSerializer.Deserialize<EstablishedTradeDefinition>(
            MessagePackSerializer.Serialize(trade));

        restored.Should().BeEquivalentTo(trade);
        restored.OriginalFills.Single().ExecutionAttemptId.Should().Be(openingAttempt);
        restored.ClosingFills.Single().ExecutionAttemptId.Should().Be(closingAttempt);
    }

    static ExecutionFillEvidence Fill(TradeLegDefinition leg, Guid componentId, Guid attempt,
        int quantity, string externalId) => new()
    {
        ExecutionFillId = Guid.NewGuid(),
        ExecutionAttemptId = attempt,
        ComponentId = componentId,
        TradeLegId = leg.TradeLegId,
        ContractId = leg.ContractId,
        SignedQuantity = quantity,
        Price = 100m,
        FilledAtUtc = new DateTime(2026, 9, 13, 14, 0, 0, DateTimeKind.Utc),
        ExternalExecutionId = externalId
    };

    static TradeOrderDefinition CreateOrder(Guid legId) => new()
    {
        PositionType = TradeOrderPositionType.Opening,
        Id = new TradeOrderId(1, 2, 3),
        Revision = 1,
        ValueDate = new DateOnly(2026, 9, 12),
        ValidUntilUtc = new DateTime(2026, 9, 12, 16, 0, 0, DateTimeKind.Utc),
        Origin = "IntegrationTest",
        DefinitionHash = "hash",
        Components = [new TradeOrderComponentDefinition
        {
            ComponentId = Guid.NewGuid(),
            ReservedTradeId = 4,
            StrategyKind = TradeStrategyKind.FuturesOutright,
            Legs = [new TradeLegDefinition
            {
                TradeLegId = legId,
                ContractId = "ESZ6",
                AssetFamily = TradeAssetFamily.Futures,
                SignedQuantity = 1,
                ContractKey = "ESZ6"
            }]
        }]
    };

    [MessagePackObject(AllowPrivate = true)]
    internal sealed record LegacyComponent(
        [property: Key(0)] Guid ComponentId,
        [property: Key(1)] TradeStrategyKind StrategyKind,
        [property: Key(2)] TradeLegDefinition[] Legs,
        [property: Key(3)] bool PermitBalancedPartialAcceptance);
}
