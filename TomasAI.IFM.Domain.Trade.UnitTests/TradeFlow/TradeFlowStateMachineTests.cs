using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Order.Execution.Model;
using TomasAI.IFM.Domain.Trade.Order.Model;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Command;
using TomasAI.IFM.Domain.Trade.Futures.Option.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Command;
using TomasAI.IFM.Domain.Trade.Futures.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Order.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class TradeFlowStateMachineTests
{
    static readonly DateTime Now = new(2026, 9, 12, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Trade_order_position_type_values_are_stable()
    {
        ((byte)TradeOrderPositionType.Unknown).Should().Be(0);
        ((byte)TradeOrderPositionType.Opening).Should().Be(1);
        ((byte)TradeOrderPositionType.Closing).Should().Be(2);
    }

    [Fact]
    public void Order_requires_complete_ownership_and_unique_stable_legs()
    {
        var invalid = Fixture.Order() with
        {
            Id = default,
            Components = [Fixture.Component(), Fixture.Component()]
        };

        var result = new TradeOrderActorStateMachine().Create(invalid);

        result.Accepted.Should().BeFalse();
        result.Detail.Should().Contain("PortfolioId").And.Contain("Duplicate ComponentId").And.Contain("Duplicate TradeLegId");
    }

    [Fact]
    public void Order_follows_approved_execution_lifecycle()
    {
        var state = new TradeOrderActorStateMachine();
        state.Create(Fixture.Order()).Accepted.Should().BeTrue();
        state.Approve().Accepted.Should().BeTrue();
        state.Ready().Accepted.Should().BeTrue();
        var executionAttemptId = Guid.NewGuid();
        state.BindExecution(executionAttemptId, ExecutionChannel.Manual, Now).Accepted.Should().BeTrue();
        state.Complete().Value!.Status.Should().Be(TradeOrderStatus.Completed);
        state.Cancel().Code.Should().Be("TO.INVALID_TRANSITION");
    }

    [Fact]
    public void Bound_execution_is_released_only_for_the_matching_attempt_with_proven_zero_exposure()
    {
        var state = new TradeOrderActorStateMachine();
        var attempt = Guid.NewGuid();
        state.Create(Fixture.Order());
        state.Approve();
        state.Ready();
        state.BindExecution(attempt, ExecutionChannel.Broker, Now).Accepted.Should().BeTrue();

        state.ReleaseExecution(Guid.NewGuid(), true, Now.AddSeconds(1)).Code
            .Should().Be("TO.EXECUTION_RELEASE_NOT_PROVEN");
        state.ReleaseExecution(attempt, false, Now.AddSeconds(1)).Code
            .Should().Be("TO.EXECUTION_RELEASE_NOT_PROVEN");

        var released = state.ReleaseExecution(attempt, true, Now.AddSeconds(1));

        released.Accepted.Should().BeTrue();
        released.Value!.Status.Should().Be(TradeOrderStatus.Ready);
        released.Value.BoundExecutionAttemptId.Should().BeNull();
    }

    [Fact]
    public void Approved_order_cannot_be_amended_without_returning_to_draft()
    {
        var state = new TradeOrderActorStateMachine();
        var order = Fixture.Order();
        state.Create(order);
        state.Approve();

        state.Amend(order with { Revision = 2 }).Code.Should().Be("TO.INVALID_TRANSITION");
    }

    [Fact]
    public void Complete_balanced_execution_creates_trade_with_original_fills_and_ownership()
    {
        var order = Fixture.ExecutingOrder();
        var state = new OrderExecutionActorStateMachine();
        var attempt = Guid.NewGuid();
        state.Start(order, attempt, ExecutionChannel.Manual, Now).Accepted.Should().BeTrue();
        foreach (var fill in Fixture.Fills(order.Components[0], attempt))
            state.AddFill(fill).Accepted.Should().BeTrue();

        var result = state.Accept(Now.AddSeconds(1));

        result.Accepted.Should().BeTrue();
        var trade = result.Value!.CreatedTrades.Should().ContainSingle().Subject;
        trade.Id.PortfolioId.Should().Be(11);
        trade.Id.FundId.Should().Be(12);
        trade.Id.TradeId.Should().Be(order.Components[0].ReservedTradeId);
        trade.OriginalFills.Should().HaveCount(4);
        trade.OriginalFills.Should().OnlyContain(fill => fill.ExecutionAttemptId == attempt);
    }

    [Fact]
    public void Complete_closing_execution_targets_existing_position_without_creating_an_opposing_trade()
    {
        var opening = Fixture.ExecutingOrder();
        var target = new StrategyPositionId(
            new TradeEntityId(opening.Id.PortfolioId, opening.Id.FundId, opening.Id.OrderId,
                opening.Components[0].ReservedTradeId), Guid.NewGuid());
        var close = opening with
        {
            Id = new(opening.Id.PortfolioId, opening.Id.FundId, opening.Id.OrderId + 1),
            PositionType = TradeOrderPositionType.Closing,
            TargetPositionId = target,
            Components =
            [
                opening.Components[0] with
                {
                    Legs = opening.Components[0].Legs.Select(leg => leg with
                    {
                        SignedQuantity = -leg.SignedQuantity
                    }).ToArray()
                }
            ]
        };
        var state = new OrderExecutionActorStateMachine();
        var attempt = Guid.NewGuid();
        state.Start(close, attempt, ExecutionChannel.Broker, Now).Accepted.Should().BeTrue();
        foreach (var fill in Fixture.Fills(close.Components[0], attempt))
            state.AddFill(fill).Accepted.Should().BeTrue();

        var result = state.Accept(Now.AddSeconds(1));

        result.Accepted.Should().BeTrue();
        result.Value!.CreatedTrades.Should().BeEmpty();
        result.Value.ClosedPositions.Should().ContainSingle().Which.PositionId.Should().Be(target);
        state.Current!.PositionType.Should().Be(TradeOrderPositionType.Closing);
        state.Current.TargetPositionId.Should().Be(target);
    }

    [Fact]
    public void Duplicate_fill_is_idempotently_ignored()
    {
        var order = Fixture.ExecutingOrder();
        var state = new OrderExecutionActorStateMachine();
        var attempt = Guid.NewGuid();
        state.Start(order, attempt, ExecutionChannel.Broker, Now);
        var fill = Fixture.Fills(order.Components[0], attempt)[0];

        state.AddFill(fill);
        state.AddFill(fill);

        state.Current!.Fills.Should().ContainSingle();
    }

    [Fact]
    public void Unbalanced_iron_condor_exposure_is_not_mislabeled_as_a_trade()
    {
        var order = Fixture.ExecutingOrder(permitPartial: true);
        var state = new OrderExecutionActorStateMachine();
        var attempt = Guid.NewGuid();
        state.Start(order, attempt, ExecutionChannel.Manual, Now);
        foreach (var fill in Fixture.Fills(order.Components[0], attempt).Take(3)) state.AddFill(fill);

        state.Accept(Now.AddSeconds(1)).Code.Should().Be("OE.UNBALANCED_EXPOSURE");
    }

    [Fact]
    public void Balanced_partial_fill_is_accepted_only_when_policy_allows_it()
    {
        var order = Fixture.ExecutingOrder(quantities: 2, permitPartial: true);
        var state = new OrderExecutionActorStateMachine();
        var attempt = Guid.NewGuid();
        state.Start(order, attempt, ExecutionChannel.Manual, Now);
        foreach (var fill in Fixture.Fills(order.Components[0], attempt, absoluteQuantity: 1)) state.AddFill(fill);

        state.Accept(Now.AddSeconds(1)).Accepted.Should().BeTrue();
    }

    [Fact]
    public void Create_option_trade_handler_applies_rules_before_appending_the_initial_event()
    {
        var trade = Fixture.Trade();
        var state = new FuturesOptionTradeCommandState();
        var create = Fixture.CreateOptionTradeCommand(trade);

        create.Execute(state).Success.Should().BeTrue();

        state.Current.Should().BeEquivalentTo(trade);
        state.Events.Should().ContainSingle()
            .Which.Should().BeOfType<OptionTradeChangedEvent>()
            .Which.IsInitialEstablishment.Should().BeTrue();
        state.AcceptChanges();

        var conflictingTrade = trade with { ExecutionAttemptId = Guid.NewGuid() };
        var result = Fixture.CreateOptionTradeCommand(conflictingTrade).Execute(state);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().StartWith("TRADE.ALREADY_EXISTS;");
        state.Events.Should().BeEmpty();
    }

    [Fact]
    public void Amend_option_trade_evidence_handler_rejects_missing_identity_and_applies_one_event()
    {
        var trade = Fixture.Trade();
        var state = Fixture.CreatedOptionTradeState(trade);
        var missingIdentity = Fixture.AmendOptionTradeEvidenceCommand(trade.Id, Guid.Empty, 2m);

        var failed = missingIdentity.Execute(state);

        failed.Success.Should().BeFalse();
        failed.ErrorMessage.Should().StartWith("TRADE.INVALID_AMENDMENT;");
        state.Events.Should().BeEmpty();

        var amendment = Fixture.AmendOptionTradeEvidenceCommand(trade.Id, Guid.NewGuid(), 2m);
        amendment.Execute(state).Success.Should().BeTrue();

        state.Current!.OpeningCommission.Should().Be(trade.OpeningCommission + 2m);
        state.Current.EvidenceRevision.Should().Be(trade.EvidenceRevision + 1);
        state.Current.Status.Should().Be(EstablishedTradeStatus.Corrected);
        state.Events.Should().ContainSingle().Which.Should().BeOfType<OptionTradeChangedEvent>();
    }

    [Fact]
    public void Begin_close_option_trade_handler_allows_only_an_active_trade()
    {
        var trade = Fixture.Trade();
        var state = Fixture.CreatedOptionTradeState(trade);
        var beginClose = Fixture.BeginCloseOptionTradeCommand(trade.Id);

        beginClose.Execute(state).Success.Should().BeTrue();

        state.Current!.Status.Should().Be(EstablishedTradeStatus.Closing);
        state.Events.Should().ContainSingle().Which.Should().BeOfType<OptionTradeChangedEvent>();
        state.AcceptChanges();

        var failed = Fixture.BeginCloseOptionTradeCommand(trade.Id).Execute(state);

        failed.Success.Should().BeFalse();
        failed.ErrorMessage.Should().StartWith("TRADE.INVALID_TRANSITION;");
        state.Events.Should().BeEmpty();
    }

    [Fact]
    public void Close_option_trade_handler_requires_closing_state_before_appending_the_event()
    {
        var trade = Fixture.Trade();
        var state = Fixture.CreatedOptionTradeState(trade);

        var failed = Fixture.CloseOptionTradeCommand(trade).Execute(state);

        failed.Success.Should().BeFalse();
        failed.ErrorMessage.Should().StartWith("TRADE.INVALID_TRANSITION;");
        state.Events.Should().BeEmpty();

        Fixture.BeginCloseOptionTradeCommand(trade.Id).Execute(state).Success.Should().BeTrue();
        state.AcceptChanges();
        Fixture.CloseOptionTradeCommand(trade).Execute(state).Success.Should().BeTrue();

        state.Current!.Status.Should().Be(EstablishedTradeStatus.Closed);
        state.Current.ClosingFills.Should().HaveCount(trade.Legs.Length);
        state.Current.ClosedAtUtc.Should().Be(Now.AddMinutes(1));
        state.Events.Should().ContainSingle().Which.Should().BeOfType<OptionTradeChangedEvent>();
    }

    [Fact]
    public void Close_option_trade_handler_rejects_incomplete_or_same_direction_fill_evidence()
    {
        var trade = Fixture.Trade();
        var state = Fixture.CreatedOptionTradeState(trade);
        Fixture.BeginCloseOptionTradeCommand(trade.Id).Execute(state).Success.Should().BeTrue();
        state.AcceptChanges();
        var invalid = Fixture.CloseOptionTradeCommand(trade) with
        {
            ClosingFills =
            [
                Fixture.CloseFill(trade, trade.Legs[0], trade.Legs[0].SignedQuantity)
            ]
        };

        var result = invalid.Execute(state);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().StartWith("TRADE.INVALID_CLOSE_EVIDENCE;");
        state.Current!.Status.Should().Be(EstablishedTradeStatus.Closing);
        state.Events.Should().BeEmpty();
    }

    [Fact]
    public void Futures_trade_handlers_apply_the_create_amend_and_close_lifecycle()
    {
        var trade = Fixture.FuturesTrade();
        var state = new FuturesTradeCommandState();

        Fixture.CreateFuturesTradeCommand(trade).Execute(state).Success.Should().BeTrue();

        state.Current.Should().BeEquivalentTo(trade);
        state.Events.Should().ContainSingle()
            .Which.Should().BeOfType<FuturesTradeChangedEvent>()
            .Which.IsInitialEstablishment.Should().BeTrue();
        state.AcceptChanges();

        var prematureClose = Fixture.CloseFuturesTradeCommand(trade).Execute(state);
        prematureClose.Success.Should().BeFalse();
        prematureClose.ErrorMessage.Should().StartWith("TRADE.INVALID_TRANSITION;");
        state.Events.Should().BeEmpty();

        Fixture.AmendFuturesTradeEvidenceCommand(trade.Id, 1.5m)
            .Execute(state).Success.Should().BeTrue();
        state.Current!.Status.Should().Be(EstablishedTradeStatus.Corrected);
        state.Current.OpeningCommission.Should().Be(trade.OpeningCommission + 1.5m);
        state.AcceptChanges();

        Fixture.BeginCloseFuturesTradeCommand(trade.Id)
            .Execute(state).Success.Should().BeTrue();
        state.Current!.Status.Should().Be(EstablishedTradeStatus.Closing);
        state.AcceptChanges();

        Fixture.CloseFuturesTradeCommand(trade).Execute(state).Success.Should().BeTrue();
        state.Current!.Status.Should().Be(EstablishedTradeStatus.Closed);
        state.Current.ClosingFills.Should().ContainSingle();
        state.Events.Should().ContainSingle().Which.Should().BeOfType<FuturesTradeChangedEvent>();
    }

    [Fact]
    public void One_leg_update_produces_a_coherent_whole_strategy_position_version()
    {
        var trade = Fixture.Trade();
        var state = new StrategyPositionActorStateMachine();
        state.Open(trade, Guid.NewGuid(), Now);
        var leg = trade.Legs[0];

        var result = state.UpdateLeg(leg.TradeLegId, 2.25m, 1, Now.AddMilliseconds(1), 1);

        result.Accepted.Should().BeTrue();
        result.Value!.Legs.Should().HaveCount(4);
        result.Value.PositionSequence.Should().Be(2);
        result.Value.Phase.Should().Be(StrategyPositionPhase.MarkToMarket);
    }

    [Fact]
    public void Stale_route_and_duplicate_tick_are_expected_rejections_without_exceptions()
    {
        var trade = Fixture.Trade();
        var state = new StrategyPositionActorStateMachine();
        state.Open(trade, Guid.NewGuid(), Now);
        var leg = trade.Legs[0];

        state.UpdateLeg(leg.TradeLegId, 2m, 1, Now.AddSeconds(1), 99).Code.Should().Be("POSITION.STALE_ROUTE");
        state.UpdateLeg(leg.TradeLegId, 2m, 1, Now.AddSeconds(1), 1).Accepted.Should().BeTrue();
        state.UpdateLeg(leg.TradeLegId, 3m, 1, Now.AddSeconds(2), 1).Code.Should().Be("POSITION.DUPLICATE_OR_OUT_OF_ORDER");
    }

    [Fact]
    public void Route_index_fans_one_contract_out_to_multiple_portfolios_and_removes_closed_position()
    {
        var index = new ContractIdRouteIndex();
        var position1 = Guid.NewGuid();
        var position2 = Guid.NewGuid();
        index.Add(Fixture.Route(position1, portfolioId: 1), "OPT-SHARED").Should().BeTrue();
        index.Add(Fixture.Route(position2, portfolioId: 2), "OPT-SHARED").Should().BeTrue();

        var outcome = index.TryRoute(new PositionMarketTick("OPT-SHARED", 10m, 1, Now), out var routes);

        outcome.Should().Be(MarketRouteLookupOutcome.Routed);
        routes.Should().HaveCount(2);
        index.RemovePosition(position1, 2).Should().Be(1);
        index.TryGetRoutes("OPT-SHARED", out routes).Should().BeTrue();
        routes.Should().ContainSingle().Which.PortfolioId.Should().Be(2);
    }

    [Fact]
    public void Valid_tick_without_open_position_is_counted_and_ignored()
    {
        var index = new ContractIdRouteIndex();
        index.RegisterKnownContract("ESZ6");

        MarketRouteLookupOutcome outcome = default;
        var action = () => outcome = index.TryRoute(new PositionMarketTick("ESZ6", 10m, 1, Now), out _);

        action.Should().NotThrow();
        outcome.Should().Be(MarketRouteLookupOutcome.NoOpenPosition);
        index.UnroutedTicks.Should().Be(1);
    }

    [Fact]
    public void Trade_order_command_handler_appends_one_replayable_event()
    {
        var state = new TradeOrderCommandState();
        var order = Fixture.Order();
        var command = new CreateTradeOrderCommand
        {
            CommandId = Guid.NewGuid(), EntityId = order.Id,
            Order = order, Subject = new ActorSubject(ActorType.Command, TradeOrderActorNames.Command,
                CreateTradeOrderCommand.Verb, order.Id.Format())
        };

        command.Execute(state).Success.Should().BeTrue();

        state.Current.Should().BeEquivalentTo(order);
        state.Events.Should().ContainSingle().Which.Should().BeOfType<TradeOrderChangedEvent>();
    }

    [Fact]
    public void Rejected_position_tick_does_not_append_an_event_or_throw()
    {
        var trade = Fixture.Trade();
        var positionId = new StrategyPositionId(trade.Id, Guid.NewGuid());
        var state = new IronCondorPositionCommandState();
        new OpenIronCondorPositionCommand
        {
            CommandId = Guid.NewGuid(), EntityId = positionId,
            Trade = trade, EffectiveAtUtc = Now,
            Subject = new ActorSubject(ActorType.Command, PositionActorNames.IronCondorCommand,
                OpenIronCondorPositionCommand.Verb, positionId.Format())
        }.Execute(state).Success.Should().BeTrue();
        state.AcceptChanges();

        var stale = new UpdateIronCondorPositionLegMarketPriceCommand
        {
            CommandId = Guid.NewGuid(), EntityId = positionId,
            TradeLegId = trade.Legs[0].TradeLegId, Price = 2m, SourceSequence = 1,
            RouteGeneration = 99, EffectiveAtUtc = Now.AddSeconds(1),
            Subject = new ActorSubject(ActorType.Command, PositionActorNames.IronCondorCommand,
                UpdateIronCondorPositionLegMarketPriceCommand.Verb, positionId.Format())
        };

        Action execute = () => stale.Execute(state).Success.Should().BeFalse();
        execute.Should().NotThrow();
        state.Events.Should().BeEmpty();
    }

    [Fact]
    public void Futures_position_command_handlers_open_and_update_one_leg_position()
    {
        var trade = Fixture.FuturesTrade();
        var positionId = new StrategyPositionId(trade.Id, Guid.NewGuid());
        var state = new FuturesPositionCommandState();
        var open = new OpenFuturesPositionCommand
        {
            CommandId = Guid.NewGuid(),
            EntityId = positionId,
            Trade = trade,
            EffectiveAtUtc = Now,
            Subject = new ActorSubject(
                ActorType.Command,
                FuturesPositionActorNames.Command,
                OpenFuturesPositionCommand.Verb,
                positionId.Format())
        };

        open.Execute(state).Success.Should().BeTrue();
        state.AcceptChanges();
        var leg = trade.Legs.Should().ContainSingle().Subject;
        var update = new UpdateFuturesPositionMarketPriceCommand
        {
            CommandId = Guid.NewGuid(),
            EntityId = positionId,
            TradeLegId = leg.TradeLegId,
            Price = 101.25m,
            SourceSequence = 42,
            RouteGeneration = 1,
            EffectiveAtUtc = Now.AddMilliseconds(1),
            Subject = new ActorSubject(
                ActorType.Command,
                FuturesPositionActorNames.Command,
                UpdateFuturesPositionMarketPriceCommand.Verb,
                positionId.Format())
        };

        update.Execute(state).Success.Should().BeTrue();

        state.Current!.PositionSequence.Should().Be(2);
        state.Current.StrategyKind.Should().Be(TradeStrategyKind.FuturesOutright);
        state.Current.Legs.Should().ContainSingle().Which.CurrentPrice.Should().Be(101.25m);
        state.Current.UnrealizedPnl.Should().Be(1.25m);
        state.Events.Should().ContainSingle().Which.Should().BeOfType<FuturesPositionChangedEvent>();
    }

    static class Fixture
    {
        public static TradeOrderDefinition Order(int quantities = 1, bool permitPartial = false) => new()
        {
            Id = new TradeOrderId(11, 12, 13),
            Revision = 1,
            Status = TradeOrderStatus.Draft,
            PositionType = TradeOrderPositionType.Opening,
            ValueDate = DateOnly.FromDateTime(Now),
            ValidUntilUtc = Now.AddMinutes(5),
            Origin = "UnitTest",
            DefinitionHash = "hash",
            Components = [Component(quantities, permitPartial)]
        };

        public static TradeOrderDefinition ExecutingOrder(int quantities = 1, bool permitPartial = false) =>
            Order(quantities, permitPartial) with { Status = TradeOrderStatus.Executing };

        public static TradeOrderComponentDefinition Component(int quantities = 1, bool permitPartial = false)
        {
            var component = Guid.Parse("10000000-0000-0000-0000-000000000001");
            return new TradeOrderComponentDefinition
            {
                ComponentId = component,
                ReservedTradeId = 21,
                StrategyKind = TradeStrategyKind.IronCondor,
                PermitBalancedPartialAcceptance = permitPartial,
                Legs = Enumerable.Range(1, 4).Select(index => new TradeLegDefinition
                {
                    TradeLegId = Guid.Parse($"20000000-0000-0000-0000-{index:000000000000}"),
                    ContractId = $"OPT-{index}",
                    AssetFamily = TradeAssetFamily.FuturesOption,
                    SignedQuantity = (index is 1 or 4 ? -1 : 1) * quantities,
                    ContractKey = $"OPT-{index}",
                    Strike = 5000 + index * 5,
                    Expiry = new DateOnly(2026, 10, 16),
                    PutCall = (byte)(index < 3 ? 0 : 1)
                }).ToArray()
            };
        }

        public static ExecutionFillEvidence[] Fills(TradeOrderComponentDefinition component, Guid attempt, int? absoluteQuantity = null) =>
            component.Legs.Select((leg, index) => new ExecutionFillEvidence
            {
                ExecutionFillId = Guid.NewGuid(),
                ExecutionAttemptId = attempt,
                ComponentId = component.ComponentId,
                TradeLegId = leg.TradeLegId,
                ContractId = leg.ContractId,
                SignedQuantity = Math.Sign(leg.SignedQuantity) * (absoluteQuantity ?? Math.Abs(leg.SignedQuantity)),
                Price = 1m + index,
                Commission = .25m,
                FilledAtUtc = Now,
                ExternalExecutionId = $"EXEC-{index}"
            }).ToArray();

        public static EstablishedTradeDefinition Trade()
        {
            var order = ExecutingOrder();
            var attempt = Guid.NewGuid();
            var component = order.Components[0];
            return new EstablishedTradeDefinition
            {
                Id = new TradeEntityId(
                    order.Id.PortfolioId,
                    order.Id.FundId,
                    order.Id.OrderId,
                    component.ReservedTradeId),
                AssetFamily = TradeAssetFamily.FuturesOption,
                StrategyKind = TradeStrategyKind.IronCondor,
                SourceComponentId = component.ComponentId,
                ExecutionAttemptId = attempt,
                Status = EstablishedTradeStatus.Open,
                Legs = component.Legs,
                OriginalFills = Fills(component, attempt),
                OpeningCommission = 1m,
                OpeningValue = 0m,
                EstablishedAtUtc = Now,
                EvidenceRevision = 1
            };
        }

        public static FuturesOptionTradeCommandState CreatedOptionTradeState(
            EstablishedTradeDefinition trade)
        {
            var state = new FuturesOptionTradeCommandState();
            CreateOptionTradeCommand(trade).Execute(state).Success.Should().BeTrue();
            state.AcceptChanges();
            return state;
        }

        public static CreateOptionTradeCommand CreateOptionTradeCommand(
            EstablishedTradeDefinition trade) => new()
            {
                CommandId = Guid.NewGuid(),
                EntityId = trade.Id,
                Trade = trade,
                Subject = OptionTradeSubject(
                    TomasAI.IFM.Domain.Trade.Shared.Futures.Option.CreateOptionTradeCommand.Verb,
                    trade.Id)
            };

        public static AmendOptionTradeEvidenceCommand AmendOptionTradeEvidenceCommand(
            TradeEntityId tradeId,
            Guid amendmentId,
            decimal commissionDelta) => new()
            {
                CommandId = Guid.NewGuid(),
                EntityId = tradeId,
                AmendmentId = amendmentId,
                CommissionDelta = commissionDelta,
                Subject = OptionTradeSubject(
                    TomasAI.IFM.Domain.Trade.Shared.Futures.Option.AmendOptionTradeEvidenceCommand.Verb,
                    tradeId)
            };

        public static BeginCloseOptionTradeCommand BeginCloseOptionTradeCommand(
            TradeEntityId tradeId) => new()
            {
                CommandId = Guid.NewGuid(),
                EntityId = tradeId,
                Subject = OptionTradeSubject(
                    TomasAI.IFM.Domain.Trade.Shared.Futures.Option.BeginCloseOptionTradeCommand.Verb,
                    tradeId)
            };

        public static CloseOptionTradeCommand CloseOptionTradeCommand(
            EstablishedTradeDefinition trade) => new()
            {
                CommandId = Guid.NewGuid(),
                EntityId = trade.Id,
                ClosingFills = ClosingFills(trade),
                ClosedAtUtc = Now.AddMinutes(1),
                Subject = OptionTradeSubject(
                    TomasAI.IFM.Domain.Trade.Shared.Futures.Option.CloseOptionTradeCommand.Verb,
                    trade.Id)
            };

        static ActorSubject OptionTradeSubject(string verb, TradeEntityId tradeId) =>
            new(
                ActorType.Command,
                FuturesOptionTradeActorNames.Command,
                verb,
                tradeId.Format());

        public static EstablishedTradeDefinition FuturesTrade()
        {
            var orderId = new TradeOrderId(11, 12, 14);
            var attempt = Guid.NewGuid();
            var leg = new TradeLegDefinition
            {
                TradeLegId = Guid.Parse("30000000-0000-0000-0000-000000000001"),
                ContractId = "ESZ6",
                AssetFamily = TradeAssetFamily.Futures,
                SignedQuantity = 1,
                ContractKey = "ESZ6",
                Expiry = new DateOnly(2026, 12, 18)
            };
            return new EstablishedTradeDefinition
            {
                Id = new TradeEntityId(orderId.PortfolioId, orderId.FundId, orderId.OrderId, 22),
                AssetFamily = TradeAssetFamily.Futures,
                StrategyKind = TradeStrategyKind.FuturesOutright,
                SourceComponentId = Guid.Parse("30000000-0000-0000-0000-000000000002"),
                ExecutionAttemptId = attempt,
                Status = EstablishedTradeStatus.Open,
                Legs = [leg],
                OriginalFills =
                [
                    new ExecutionFillEvidence
                    {
                        ExecutionFillId = Guid.NewGuid(),
                        ExecutionAttemptId = attempt,
                        ComponentId = Guid.Parse("30000000-0000-0000-0000-000000000002"),
                        TradeLegId = leg.TradeLegId,
                        ContractId = leg.ContractId,
                        SignedQuantity = 1,
                        Price = 100m,
                        FilledAtUtc = Now,
                        ExternalExecutionId = "FUTURES-EXEC-1"
                    }
                ],
                OpeningValue = 100m,
                EstablishedAtUtc = Now,
                EvidenceRevision = 1
            };
        }

        public static CreateFuturesTradeCommand CreateFuturesTradeCommand(
            EstablishedTradeDefinition trade) => new()
            {
                CommandId = Guid.NewGuid(),
                EntityId = trade.Id,
                Trade = trade,
                Subject = FuturesTradeSubject(
                    TomasAI.IFM.Domain.Trade.Shared.Futures.CreateFuturesTradeCommand.Verb,
                    trade.Id)
            };

        public static AmendFuturesTradeEvidenceCommand AmendFuturesTradeEvidenceCommand(
            TradeEntityId tradeId,
            decimal commissionDelta) => new()
            {
                CommandId = Guid.NewGuid(),
                EntityId = tradeId,
                AmendmentId = Guid.NewGuid(),
                CommissionDelta = commissionDelta,
                Subject = FuturesTradeSubject(
                    TomasAI.IFM.Domain.Trade.Shared.Futures.AmendFuturesTradeEvidenceCommand.Verb,
                    tradeId)
            };

        public static BeginCloseFuturesTradeCommand BeginCloseFuturesTradeCommand(
            TradeEntityId tradeId) => new()
            {
                CommandId = Guid.NewGuid(),
                EntityId = tradeId,
                Subject = FuturesTradeSubject(
                    TomasAI.IFM.Domain.Trade.Shared.Futures.BeginCloseFuturesTradeCommand.Verb,
                    tradeId)
            };

        public static CloseFuturesTradeCommand CloseFuturesTradeCommand(
            EstablishedTradeDefinition trade) => new()
            {
                CommandId = Guid.NewGuid(),
                EntityId = trade.Id,
                ClosingFills = ClosingFills(trade),
                ClosedAtUtc = Now.AddMinutes(1),
                Subject = FuturesTradeSubject(
                    TomasAI.IFM.Domain.Trade.Shared.Futures.CloseFuturesTradeCommand.Verb,
                    trade.Id)
            };

        static ExecutionFillEvidence[] ClosingFills(EstablishedTradeDefinition trade)
        {
            var attempt = Guid.NewGuid();
            return trade.Legs.Select((leg, index) => new ExecutionFillEvidence
            {
                ExecutionFillId = Guid.NewGuid(),
                ExecutionAttemptId = attempt,
                ComponentId = trade.SourceComponentId,
                TradeLegId = leg.TradeLegId,
                ContractId = leg.ContractId,
                SignedQuantity = -leg.SignedQuantity,
                Price = 1.5m + index,
                FilledAtUtc = Now.AddMinutes(1),
                ExternalExecutionId = $"CLOSE-{index}"
            }).ToArray();
        }

        public static ExecutionFillEvidence CloseFill(
            EstablishedTradeDefinition trade, TradeLegDefinition leg, int signedQuantity) => new()
        {
            ExecutionFillId = Guid.NewGuid(),
            ExecutionAttemptId = Guid.NewGuid(),
            ComponentId = trade.SourceComponentId,
            TradeLegId = leg.TradeLegId,
            ContractId = leg.ContractId,
            SignedQuantity = signedQuantity,
            Price = 1.5m,
            FilledAtUtc = Now.AddMinutes(1),
            ExternalExecutionId = "INVALID-CLOSE"
        };

        static ActorSubject FuturesTradeSubject(string verb, TradeEntityId tradeId) =>
            new(
                ActorType.Command,
                FuturesTradeActorNames.Command,
                verb,
                tradeId.Format());

        public static PortfolioFundTradeLeg Route(Guid positionId, int portfolioId) => new(
            portfolioId, 2, 3, 4, positionId, Guid.NewGuid(), TradeStrategyKind.IronCondor, 1);
    }
}
