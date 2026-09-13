using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Order.Execution.Model;
using TomasAI.IFM.Domain.Trade.Order.Model;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Shared.Model;

namespace TomasAI.IFM.Domain.Trade.BDDTests.TradeFlow;

/// <summary>Executable actor-level trade lifecycle behavior.</summary>
public sealed class TradeFlowBehaviorTests
{
    static readonly DateTime Now = new(2026, 9, 12, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Approved_order_to_execution_to_option_trade_to_open_strategy_position()
    {
        var legIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var componentId = Guid.NewGuid();
        var orderDefinition = new TradeOrderDefinition
        {
            Id = new TradeOrderId(100, 200, 300),
            Revision = 1,
            ValueDate = DateOnly.FromDateTime(Now),
            ValidUntilUtc = Now.AddMinutes(5),
            Origin = "StrategyWorkflow",
            DefinitionHash = "bdd-definition",
            Components = [new TradeOrderComponentDefinition
            {
                ComponentId = componentId,
                ReservedTradeId = 401,
                StrategyKind = TradeStrategyKind.IronCondor,
                Legs = legIds.Select((id, index) => new TradeLegDefinition
                {
                    TradeLegId = id,
                    MarketInstrumentId = (uint)(9100 + index),
                    AssetFamily = TradeAssetFamily.FuturesOption,
                    SignedQuantity = index is 0 or 3 ? -1 : 1,
                    ContractKey = $"ES-OPTION-{index}"
                }).ToArray()
            }]
        };

        var order = new TradeOrderActorStateMachine();
        order.Create(orderDefinition).Accepted.Should().BeTrue();
        order.Approve().Accepted.Should().BeTrue();
        order.Ready().Accepted.Should().BeTrue();
        var attempt = Guid.NewGuid();
        var executing = order.BindExecution(attempt, ExecutionChannel.Manual, Now).Value!;

        var execution = new OrderExecutionActorStateMachine();
        execution.Start(executing, attempt, ExecutionChannel.Manual, Now).Accepted.Should().BeTrue();
        execution.MarkSubmitted().Accepted.Should().BeTrue();
        foreach (var leg in executing.Components[0].Legs)
        {
            execution.AddFill(new ExecutionFillEvidence
            {
                ExecutionFillId = Guid.NewGuid(),
                ExecutionAttemptId = attempt,
                ExternalExecutionId = $"MANUAL-{leg.MarketInstrumentId}",
                ComponentId = componentId,
                TradeLegId = leg.TradeLegId,
                MarketInstrumentId = leg.MarketInstrumentId,
                SignedQuantity = leg.SignedQuantity,
                Price = 2m,
                Commission = .25m,
                FilledAtUtc = Now.AddSeconds(1)
            }).Accepted.Should().BeTrue();
        }
        var optionTrade = execution.Accept(Now.AddSeconds(2)).Value!.Single();
        optionTrade.Id.PortfolioId.Should().Be(100);
        optionTrade.Id.FundId.Should().Be(200);

        var position = new StrategyPositionActorStateMachine();
        var opened = position.Open(optionTrade, Guid.NewGuid(), Now.AddSeconds(3)).Value!;
        opened.IsOpen.Should().BeTrue();
        opened.Legs.Should().HaveCount(4);

        var routes = new MarketInstrumentRouteIndex();
        foreach (var leg in opened.Legs)
            routes.Add(new MarketPositionRoute(
                opened.Id.Trade.PortfolioId, opened.Id.Trade.FundId, opened.Id.Trade.OrderId,
                opened.Id.Trade.TradeId, opened.Id.PositionId, leg.TradeLegId, opened.StrategyKind,
                "FuturesIronCondorTradePositionCommand", opened.Id.Format(), opened.RouteGeneration),
                leg.MarketInstrumentId).Should().BeTrue();

        var changedLeg = opened.Legs[0];
        routes.TryRoute(new PositionMarketTick(changedLeg.MarketInstrumentId, 2.5m, 1, Now.AddSeconds(4)), out var destinations)
            .Should().Be(MarketRouteLookupOutcome.Routed);
        destinations.Should().ContainSingle();
        position.UpdateLeg(destinations[0].TradeLegId, 2.5m, 1, Now.AddSeconds(4), destinations[0].Generation)
            .Value!.PositionSequence.Should().Be(2);
    }

    [Fact]
    public void Tick_before_open_and_after_close_is_observed_and_ignored()
    {
        var index = new MarketInstrumentRouteIndex();
        index.RegisterKnownInstrument(7001);
        index.TryRoute(new PositionMarketTick(7001, 100m, 1, Now), out _)
            .Should().Be(MarketRouteLookupOutcome.NoOpenPosition);

        var positionId = Guid.NewGuid();
        index.Add(new MarketPositionRoute(1, 2, 3, 4, positionId, Guid.NewGuid(),
            TradeStrategyKind.FuturesOutright, "FuturesTradePositionCommand", "1.2.3.4", 1), 7001);
        index.RemovePosition(positionId).Should().Be(1);

        index.TryRoute(new PositionMarketTick(7001, 101m, 2, Now.AddMilliseconds(1)), out _)
            .Should().Be(MarketRouteLookupOutcome.NoOpenPosition);
    }

    [Fact]
    public void Incomplete_vertical_spread_remains_partial_and_cannot_establish_a_trade()
    {
        var order = VerticalOrder(quantity: 1, permitPartial: false);
        var execution = new OrderExecutionActorStateMachine();
        var attempt = Guid.NewGuid();
        execution.Start(order, attempt, ExecutionChannel.Manual, Now);
        execution.AddFill(Fill(order.Components[0], order.Components[0].Legs[0], attempt,
            Math.Sign(order.Components[0].Legs[0].SignedQuantity)));

        execution.Current!.Status.Should().Be(OrderExecutionStatus.PartiallyFilled);
        execution.Accept(Now.AddSeconds(1)).Code.Should().Be("OE.UNBALANCED_EXPOSURE");
    }

    [Fact]
    public void Balanced_partial_vertical_spread_can_establish_one_trade_when_policy_allows()
    {
        var order = VerticalOrder(quantity: 2, permitPartial: true);
        var execution = new OrderExecutionActorStateMachine();
        var attempt = Guid.NewGuid();
        execution.Start(order, attempt, ExecutionChannel.Broker, Now);
        foreach (var leg in order.Components[0].Legs)
            execution.AddFill(Fill(order.Components[0], leg, attempt, Math.Sign(leg.SignedQuantity)));

        execution.Accept(Now.AddSeconds(1)).Value.Should().ContainSingle()
            .Which.StrategyKind.Should().Be(TradeStrategyKind.VerticalSpread);
    }

    [Fact]
    public void Restart_snapshot_restores_shared_contract_fanout_and_generation_fences_old_route()
    {
        var oldPosition = Guid.NewGuid();
        var currentPosition = Guid.NewGuid();
        var leg1 = Guid.NewGuid();
        var leg2 = Guid.NewGuid();
        var recovered = new[]
        {
            (8001u, new MarketPositionRoute(1, 10, 20, 30, oldPosition, leg1,
                TradeStrategyKind.IronCondor, "Iron", "old", 1)),
            (8001u, new MarketPositionRoute(2, 11, 21, 31, currentPosition, leg2,
                TradeStrategyKind.VerticalSpread, "Vertical", "current", 2))
        };
        var index = new MarketInstrumentRouteIndex();

        index.ReplaceFromSnapshot(recovered);
        index.RemovePosition(oldPosition).Should().Be(1);
        index.TryRoute(new PositionMarketTick(8001, 2m, 1, Now), out var routes)
            .Should().Be(MarketRouteLookupOutcome.Routed);

        routes.Should().ContainSingle().Which.Generation.Should().Be(2);
        routes[0].PortfolioId.Should().Be(2);
    }

    static TradeOrderDefinition VerticalOrder(int quantity, bool permitPartial)
    {
        var component = new TradeOrderComponentDefinition
        {
            ComponentId = Guid.NewGuid(), ReservedTradeId = 88,
            StrategyKind = TradeStrategyKind.VerticalSpread,
            PermitBalancedPartialAcceptance = permitPartial,
            Legs =
            [
                new() { TradeLegId = Guid.NewGuid(), MarketInstrumentId = 1, AssetFamily = TradeAssetFamily.FuturesOption, SignedQuantity = -quantity, ContractKey = "A" },
                new() { TradeLegId = Guid.NewGuid(), MarketInstrumentId = 2, AssetFamily = TradeAssetFamily.FuturesOption, SignedQuantity = quantity, ContractKey = "B" }
            ]
        };
        return new TradeOrderDefinition
        {
            Id = new TradeOrderId(1, 2, 3), Revision = 1,
            Status = TradeOrderStatus.Executing, ValueDate = DateOnly.FromDateTime(Now),
            ValidUntilUtc = Now.AddMinutes(1), Origin = "BDD", DefinitionHash = "vertical",
            Components = [component]
        };
    }

    static ExecutionFillEvidence Fill(TradeOrderComponentDefinition component, TradeLegDefinition leg,
        Guid attempt, int quantity) => new()
    {
        ExecutionFillId = Guid.NewGuid(), ExecutionAttemptId = attempt,
        ExternalExecutionId = Guid.NewGuid().ToString("N"), ComponentId = component.ComponentId,
        TradeLegId = leg.TradeLegId, MarketInstrumentId = leg.MarketInstrumentId,
        SignedQuantity = quantity, Price = 1m, FilledAtUtc = Now
    };
}
