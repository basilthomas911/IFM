using TomasAI.IFM.Domain.Trade.Order.Broker.Command;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Broker.Query.Model;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit;

namespace TomasAI.IFM.Application.TradeBroker.UnitTests;

public sealed class BrokerOrderCommandTests
{
    [Fact]
    public void Create_commits_one_pending_intent_and_exact_replay_adds_no_event()
    {
        var command = CreateCommand();
        var state = new BrokerOrderCommandState();
        Assert.True(ExecuteCreate(command, state).Success);
        Assert.Equal(BrokerOrderStatus.PlacePending, state.Current!.Status);
        Assert.Single(state.Events);
        Assert.True(ExecuteCreate(command, state).Success);
        Assert.Single(state.Events);
    }

    [Fact]
    public void Receipt_must_match_operation_and_exact_replay_adds_no_event()
    {
        var create = CreateCommand();
        var state = new BrokerOrderCommandState();
        Assert.True(ExecuteCreate(create, state).Success);
        var receipt = new RecordBrokerDispatchCommand
        {
            CommandId = Guid.NewGuid(), EntityId = create.EntityId,
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command, RecordBrokerDispatchCommand.Verb, create.EntityId.Format()),
            OperationId = create.OperationId, Outcome = BrokerDispatchResult.AcceptedForDispatch,
            Category = "EM.DISPATCHED", Detail = "accepted", RecordedAtUtc = create.EffectiveAtUtc.AddMilliseconds(1)
        };
        Assert.True(receipt.Execute(state).Success);
        Assert.Equal(BrokerOrderStatus.Dispatched, state.Current!.Status);
        Assert.Equal(2, state.Events.Count);
        Assert.True(receipt.Execute(state).Success);
        Assert.Equal(2, state.Events.Count);
        Assert.False((receipt with { CommandId = Guid.NewGuid(), OperationId = Guid.NewGuid() }).Execute(state).Success);
        Assert.Equal(2, state.Events.Count);
    }

    [Fact]
    public void Broker_observation_is_durable_idempotent_and_changed_content_fails_closed()
    {
        var create = CreateCommand();
        var state = new BrokerOrderCommandState();
        Assert.True(ExecuteCreate(create, state).Success);
        var dispatch = new RecordBrokerDispatchCommand
        {
            CommandId = Guid.NewGuid(), EntityId = create.EntityId,
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command,
                RecordBrokerDispatchCommand.Verb, create.EntityId.Format()),
            OperationId = create.OperationId,
            Outcome = BrokerDispatchResult.AcceptedForDispatch,
            Category = "EM.DISPATCHED", Detail = "accepted",
            RecordedAtUtc = create.EffectiveAtUtc.AddMilliseconds(1)
        };
        Assert.True(dispatch.Execute(state).Success);
        var evidence = new BrokerOrderObservationEvidence
        {
            ObservationId = Guid.NewGuid(),
            Kind = BrokerOrderObservationKind.Acknowledged,
            AccountAlias = "EMU",
            OperationId = create.OperationId,
            ComponentId = create.EntityId.ComponentId,
            OrderRevision = 1,
            SourceEpoch = 1,
            SourceSequence = 1,
            OccurredAtUtc = create.EffectiveAtUtc.AddMilliseconds(2),
            ContentHash = "HASH-A"
        };
        var observed = new RecordBrokerOrderObservationCommand
        {
            CommandId = Guid.NewGuid(), EntityId = create.EntityId, Observation = evidence,
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command,
                RecordBrokerOrderObservationCommand.Verb, create.EntityId.Format())
        };
        Assert.True(observed.Execute(state).Success);
        Assert.Equal(BrokerOrderStatus.Working, state.Current!.Status);
        Assert.Equal(3, state.Events.Count);
        Assert.True(observed.Execute(state).Success);
        Assert.Equal(3, state.Events.Count);
        Assert.False((observed with
        {
            CommandId = Guid.NewGuid(),
            Observation = evidence with { ContentHash = "HASH-B" }
        }).Execute(state).Success);
        Assert.Equal(3, state.Events.Count);
    }

    [Fact]
    public void Authoritative_ack_before_local_receipt_does_not_downgrade_working_state()
    {
        var create = CreateCommand();
        var state = new BrokerOrderCommandState();
        Assert.True(ExecuteCreate(create, state).Success);
        var evidence = new BrokerOrderObservationEvidence
        {
            ObservationId = Guid.NewGuid(), Kind = BrokerOrderObservationKind.Acknowledged,
            AccountAlias = "EMU", OperationId = create.OperationId,
            ComponentId = create.EntityId.ComponentId, OrderRevision = 1,
            SourceEpoch = 1, SourceSequence = 1,
            OccurredAtUtc = create.EffectiveAtUtc.AddMilliseconds(1), ContentHash = "ACK"
        };
        var observed = new RecordBrokerOrderObservationCommand
        {
            CommandId = Guid.NewGuid(), EntityId = create.EntityId, Observation = evidence,
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command,
                RecordBrokerOrderObservationCommand.Verb, create.EntityId.Format())
        };
        Assert.True(observed.Execute(state).Success);
        var receipt = new RecordBrokerDispatchCommand
        {
            CommandId = Guid.NewGuid(), EntityId = create.EntityId,
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command,
                RecordBrokerDispatchCommand.Verb, create.EntityId.Format()),
            OperationId = create.OperationId, Outcome = BrokerDispatchResult.AcceptedForDispatch,
            Category = "EM.DISPATCHED", Detail = "accepted",
            RecordedAtUtc = create.EffectiveAtUtc.AddMilliseconds(2)
        };
        Assert.True(receipt.Execute(state).Success);
        Assert.Equal(BrokerOrderStatus.Working, state.Current!.Status);
        Assert.Null(state.Current.LastObservation);
    }

    [Fact]
    public void Working_order_records_price_change_and_cancel_as_separate_durable_mutations()
    {
        var create = CreateCommand();
        var state = CreateWorkingState(create);
        state.AcceptChanges();
        var update = new RequestBrokerOrderLimitUpdateCommand
        {
            CommandId = Guid.NewGuid(), EntityId = create.EntityId,
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command,
                RequestBrokerOrderLimitUpdateCommand.Verb, create.EntityId.Format()),
            OperationId = Guid.NewGuid(), NewSignedNetDebitLimit = 99.5m,
            EffectiveAtUtc = create.EffectiveAtUtc.AddSeconds(1)
        };

        Assert.True(update.Execute(state).Success);
        Assert.Equal(BrokerOrderStatus.UpdatePending, state.Current!.Status);
        Assert.Equal(BrokerMutationKind.UpdateLimit, state.Current.PendingMutation);
        Assert.Single(state.Events);
        state.AcceptChanges();
        var updateReceipt = new RecordBrokerDispatchCommand
        {
            CommandId = Guid.NewGuid(), EntityId = create.EntityId,
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command,
                RecordBrokerDispatchCommand.Verb, create.EntityId.Format()),
            OperationId = update.OperationId, Outcome = BrokerDispatchResult.AcceptedForDispatch,
            Category = "EM.MODIFIED", Detail = "changed",
            RecordedAtUtc = create.EffectiveAtUtc.AddSeconds(2)
        };
        Assert.True(updateReceipt.Execute(state).Success);
        Assert.Equal(BrokerOrderStatus.Working, state.Current!.Status);
        state.AcceptChanges();
        var cancel = new RequestBrokerOrderCancelCommand
        {
            CommandId = Guid.NewGuid(), EntityId = create.EntityId,
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command,
                RequestBrokerOrderCancelCommand.Verb, create.EntityId.Format()),
            OperationId = Guid.NewGuid(), EffectiveAtUtc = create.EffectiveAtUtc.AddSeconds(3)
        };
        Assert.True(cancel.Execute(state).Success);
        Assert.Equal(BrokerOrderStatus.CancelPending, state.Current!.Status);
        Assert.Equal(BrokerMutationKind.Cancel, state.Current.PendingMutation);
    }

    [Fact]
    public void Partially_filled_order_can_update_remaining_limit_and_then_cancel_remaining_quantity()
    {
        var create = CreateCommand();
        var state = CreateWorkingState(create);
        var partial = new RecordBrokerOrderObservationCommand
        {
            CommandId = Guid.NewGuid(), EntityId = create.EntityId,
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command,
                RecordBrokerOrderObservationCommand.Verb, create.EntityId.Format()),
            Observation = new BrokerOrderObservationEvidence
            {
                ObservationId = Guid.NewGuid(), Kind = BrokerOrderObservationKind.Execution,
                AccountAlias = "EMU", OperationId = create.OperationId,
                ComponentId = create.EntityId.ComponentId, LegId = Guid.NewGuid(),
                ContractId = "ES", ExternalExecutionId = "EM-PARTIAL-1",
                SignedQuantity = 1, Price = 100m, OrderRevision = 1,
                SourceEpoch = 1, SourceSequence = 2,
                OccurredAtUtc = create.EffectiveAtUtc.AddSeconds(1), ContentHash = "PARTIAL"
            }
        };
        Assert.True(partial.Execute(state).Success);
        Assert.Equal(BrokerOrderStatus.PartiallyFilled, state.Current!.Status);

        var update = new RequestBrokerOrderLimitUpdateCommand
        {
            CommandId = Guid.NewGuid(), EntityId = create.EntityId,
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command,
                RequestBrokerOrderLimitUpdateCommand.Verb, create.EntityId.Format()),
            OperationId = Guid.NewGuid(), NewSignedNetDebitLimit = 99.5m,
            EffectiveAtUtc = create.EffectiveAtUtc.AddSeconds(2)
        };
        Assert.True(update.Execute(state).Success);
        Assert.Equal(BrokerOrderStatus.UpdatePending, state.Current!.Status);
        Assert.True(new RecordBrokerDispatchCommand
        {
            CommandId = Guid.NewGuid(), EntityId = create.EntityId,
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command,
                RecordBrokerDispatchCommand.Verb, create.EntityId.Format()),
            OperationId = update.OperationId, Outcome = BrokerDispatchResult.AcceptedForDispatch,
            Category = "EM.MODIFIED", Detail = "remaining quantity updated",
            RecordedAtUtc = create.EffectiveAtUtc.AddSeconds(3)
        }.Execute(state).Success);
        Assert.Equal(BrokerOrderStatus.PartiallyFilled, state.Current!.Status);

        var cancel = new RequestBrokerOrderCancelCommand
        {
            CommandId = Guid.NewGuid(), EntityId = create.EntityId,
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command,
                RequestBrokerOrderCancelCommand.Verb, create.EntityId.Format()),
            OperationId = Guid.NewGuid(), EffectiveAtUtc = create.EffectiveAtUtc.AddSeconds(4)
        };
        Assert.True(cancel.Execute(state).Success);
        Assert.True(new RecordBrokerOrderObservationCommand
        {
            CommandId = Guid.NewGuid(), EntityId = create.EntityId,
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command,
                RecordBrokerOrderObservationCommand.Verb, create.EntityId.Format()),
            Observation = partial.Observation with
            {
                ObservationId = Guid.NewGuid(), Kind = BrokerOrderObservationKind.Cancelled,
                OperationId = cancel.OperationId, ExternalExecutionId = null,
                SignedQuantity = 0, Price = 0, SourceSequence = 3,
                OccurredAtUtc = create.EffectiveAtUtc.AddSeconds(5), ContentHash = "CANCELLED"
            }
        }.Execute(state).Success);
        Assert.Equal(BrokerOrderStatus.Cancelled, state.Current!.Status);

        Assert.True(new RecordBrokerOrderObservationCommand
        {
            CommandId = Guid.NewGuid(), EntityId = create.EntityId,
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command,
                RecordBrokerOrderObservationCommand.Verb, create.EntityId.Format()),
            Observation = partial.Observation with
            {
                ObservationId = Guid.NewGuid(), OperationId = update.OperationId,
                ExternalExecutionId = "EM-LATE-1", SourceSequence = 4,
                OccurredAtUtc = create.EffectiveAtUtc.AddSeconds(6), ContentHash = "LATE-FILL"
            }
        }.Execute(state).Success);
        Assert.Equal(BrokerOrderStatus.PartiallyFilled, state.Current!.Status);
    }

    [Fact]
    public void Opening_order_requires_the_exact_accepted_account_approval_but_closing_can_reduce_risk_while_gate_is_closed()
    {
        var create = CreateCommand();
        var approval = Guid.NewGuid();
        create = create with
        {
            Order = create.Order with { AccountPromotionApprovalReference = approval.ToString("N") }
        };
        var store = new BrokerAccountReadStore();
        store.Set(new BrokerAccountDefinition
        {
            Id = new("EMU"),
            Environment = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment.Emulator,
            Snapshot = new BrokerAccountSnapshotEvidence
            {
                AccountAlias = "EMU", Currency = "USD", Complete = true,
                NewRiskAllowed = true, Generation = 1,
                AsOfUtc = create.EffectiveAtUtc
            },
            QualificationStatus = BrokerAccountQualificationStatus.ReviewPending,
            Gate = BrokerAccountOperationalGate.Closed,
            ApprovalId = approval
        });
        var state = new BrokerOrderCommandState();
        Assert.False(create.Execute(state, store).Success);
        store.Set(store.Get(new("EMU"))! with
        {
            QualificationStatus = BrokerAccountQualificationStatus.Accepted,
            Gate = BrokerAccountOperationalGate.Open
        });
        Assert.True(create.Execute(state, store).Success);

        var closing = CreateCommand();
        closing = closing with
        {
            Order = closing.Order with
            {
                PositionType = TradeOrderPositionType.Closing,
                BrokerAccountAlias = "EMU",
                TargetPositionId = new(new(1, 2, 3, 4), Guid.NewGuid())
            }
        };
        Assert.True(closing.Execute(new BrokerOrderCommandState(), store).Success);
    }

    [Fact]
    public void Read_projection_lists_only_components_for_the_exact_trade_order()
    {
        var store = new BrokerOrderReadStore();
        var first = CreateCommand();
        var second = CreateCommand();
        second = second with
        {
            EntityId = new BrokerOrderId(
                new(first.EntityId.Execution.TradeOrder, Guid.NewGuid()), Guid.NewGuid())
        };
        var foreign = CreateCommand();
        foreign = foreign with
        {
            EntityId = new BrokerOrderId(
                new(new TradeOrderId(9, 9, 9), Guid.NewGuid()), Guid.NewGuid())
        };
        store.Set(new BrokerOrderDefinition { Id = first.EntityId, Status = BrokerOrderStatus.Working });
        store.Set(new BrokerOrderDefinition { Id = second.EntityId, Status = BrokerOrderStatus.Filled });
        store.Set(new BrokerOrderDefinition { Id = foreign.EntityId, Status = BrokerOrderStatus.Cancelled });

        var values = store.List(first.EntityId.Execution.TradeOrder);

        Assert.Equal(2, values.Length);
        Assert.Contains(values, value => value.Id == first.EntityId);
        Assert.Contains(values, value => value.Id == second.EntityId);
        Assert.DoesNotContain(values, value => value.Id == foreign.EntityId);
    }

    private static BrokerOrderCommandState CreateWorkingState(CreateBrokerOrderCommand create)
    {
        var state = new BrokerOrderCommandState();
        Assert.True(ExecuteCreate(create, state).Success);
        Assert.True(new RecordBrokerOrderObservationCommand
        {
            CommandId = Guid.NewGuid(), EntityId = create.EntityId,
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command,
                RecordBrokerOrderObservationCommand.Verb, create.EntityId.Format()),
            Observation = new BrokerOrderObservationEvidence
            {
                ObservationId = Guid.NewGuid(), Kind = BrokerOrderObservationKind.Acknowledged,
                AccountAlias = "EMU", OperationId = create.OperationId,
                ComponentId = create.EntityId.ComponentId, OrderRevision = 1,
                SourceEpoch = 1, SourceSequence = 1,
                OccurredAtUtc = create.EffectiveAtUtc.AddMilliseconds(1), ContentHash = "ACK"
            }
        }.Execute(state).Success);
        return state;
    }

    private static CreateBrokerOrderCommand CreateCommand()
    {
        var attempt = Guid.NewGuid();
        var component = Guid.NewGuid();
        var approvalId = Guid.NewGuid();
        var order = new TradeOrderDefinition
        {
            Id = new(1, 2, 3), Revision = 1, Status = TradeOrderStatus.Approved,
            PositionType = TradeOrderPositionType.Opening, PortfolioApprovalId = Guid.NewGuid(),
            BrokerAccountAlias = "EMU", BrokerEnvironment = BrokerEnvironment.Emulator,
            AccountPromotionApprovalReference = approvalId.ToString("N"),
            DefinitionHash = "definition", MicroExecutionProfileHash = "profile",
            RequiredCapital = 1_000m, MaximumLoss = 1_000m,
            ValidUntilUtc = new DateTime(2026, 9, 16, 14, 5, 0, DateTimeKind.Utc),
            Components = [new TradeOrderComponentDefinition
            {
                ComponentId = component, ReservedTradeId = 4, StrategyKind = TradeStrategyKind.FuturesOutright,
                SignedNetDebitLimit = 100m, MinimumSignedNetDebitLimit = 90m,
                MaximumSignedNetDebitLimit = 110m, TickIncrement = 0.25m,
                Legs = [new TradeLegDefinition { TradeLegId = Guid.NewGuid(), ContractId = "ES", SignedQuantity = 1, CashMultiplier = 50m }]
            }]
        };
        var id = new BrokerOrderId(new(order.Id, attempt), component);
        return new CreateBrokerOrderCommand
        {
            CommandId = Guid.NewGuid(), EntityId = id, OperationId = Guid.NewGuid(), Order = order,
            EffectiveAtUtc = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc),
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command, CreateBrokerOrderCommand.Verb, id.Format())
        };
    }

    private static TomasAI.IFM.Shared.EventSourcing.ServiceResult<
        TomasAI.IFM.Shared.EventSourcing.GuidResult> ExecuteCreate(
        CreateBrokerOrderCommand command, BrokerOrderCommandState state)
    {
        var approvalId = Guid.ParseExact(command.Order.AccountPromotionApprovalReference, "N");
        var accounts = new BrokerAccountReadStore();
        accounts.Set(new BrokerAccountDefinition
        {
            Id = new(command.Order.BrokerAccountAlias),
            Environment = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment.Emulator,
            Snapshot = new BrokerAccountSnapshotEvidence
            {
                AccountAlias = command.Order.BrokerAccountAlias,
                Currency = "USD", Complete = true, NewRiskAllowed = true,
                Generation = 1, AsOfUtc = command.EffectiveAtUtc
            },
            QualificationStatus = BrokerAccountQualificationStatus.Accepted,
            Gate = BrokerAccountOperationalGate.Open,
            ApprovalId = approvalId
        });
        return command.Execute(state, accounts);
    }
}
