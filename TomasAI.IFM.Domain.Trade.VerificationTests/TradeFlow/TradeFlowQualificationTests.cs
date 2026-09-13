using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Futures.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Query.Actor;
using TomasAI.IFM.Domain.Trade.Order.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Execution.Query.Actor;
using TomasAI.IFM.Domain.Trade.Order.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.VerificationTests.TradeFlow;

public sealed class TradeFlowQualificationTests
{
    static readonly DateTime Now = new(2026, 9, 12, 16, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Every_wire_contract_has_an_explicit_messagepack_shape()
    {
        Type[] contracts =
        [
            typeof(TradeOrderId), typeof(OrderExecutionId), typeof(TradeEntityId),
            typeof(StrategyPositionId), typeof(TradeLegDefinition),
            typeof(TradeOrderComponentDefinition), typeof(TradeOrderDefinition), typeof(ExecutionFillEvidence),
            typeof(OrderExecutionDefinition), typeof(EstablishedTradeDefinition), typeof(StrategyPositionLeg),
            typeof(StrategyPositionSnapshot), typeof(PortfolioFundTradeLeg),
            typeof(CreateTradeOrderCommand), typeof(ApproveTradeOrderCommand),
            typeof(ReleaseTradeOrderExecutionCommand), typeof(TradeOrderChangedEvent),
            typeof(StartOrderExecutionCommand), typeof(AddOrderExecutionFillCommand), typeof(OrderExecutionChangedEvent),
            typeof(CreateFuturesTradeCommand), typeof(FuturesTradeChangedEvent),
            typeof(OpenFuturesPositionCommand), typeof(UpdateFuturesPositionMarketPriceCommand),
            typeof(FuturesPositionChangedEvent),
            typeof(CreateOptionTradeCommand), typeof(OptionTradeChangedEvent),
            typeof(OpenIronCondorPositionCommand), typeof(UpdateIronCondorPositionLegMarketPriceCommand),
            typeof(OpenVerticalSpreadPositionCommand), typeof(UpdateVerticalSpreadPositionLegMarketPriceCommand),
            typeof(IronCondorPositionChangedEvent), typeof(VerticalSpreadPositionChangedEvent),
            typeof(StrategyPositionOpenedEvent), typeof(StrategyPositionClosedEvent),
            typeof(StrategyPositionCorrectedEvent), typeof(OpenPositionRoutesChangedEvent),
            typeof(GetTradeOrderQuery), typeof(GetOrderExecutionQuery),
            typeof(GetIronCondorOptionTradeQuery), typeof(GetVerticalSpreadOptionTradeQuery),
            typeof(GetFuturesTradeQuery), typeof(GetFuturesTradePositionQuery),
            typeof(GetIronCondorOptionTradePositionQuery)
        ];

        contracts.Should().OnlyContain(type =>
            type.GetCustomAttributes(typeof(MessagePackObjectAttribute), false).Length == 1);
    }

    [Fact]
    public void Valid_unrouted_tick_hot_path_allocates_zero_bytes()
    {
        var index = new ContractIdRouteIndex(8);
        index.RegisterKnownContract("ESZ6");
        for (var sequence = 1; sequence <= 100; sequence++)
            index.TryRoute(new PositionMarketTick("ESZ6", 10m, sequence, Now), out _);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var sequence = 101; sequence <= 10_100; sequence++)
            index.TryRoute(new PositionMarketTick("ESZ6", 10m, sequence, Now), out _);

        (GC.GetAllocatedBytesForCurrentThread() - before).Should().Be(0);
    }

    [Fact]
    public void Routed_tick_reuses_the_prebuilt_route_bucket()
    {
        var index = new ContractIdRouteIndex(8);
        index.Add(new PortfolioFundTradeLeg(1, 2, 3, 4, Guid.NewGuid(), Guid.NewGuid(),
            TradeStrategyKind.IronCondor, 1), "ESZ6-C5000");
        index.TryGetRoutes("ESZ6-C5000", out var expected);

        index.TryRoute(new PositionMarketTick("ESZ6-C5000", 10m, 1, Now), out var actual)
            .Should().Be(MarketRouteLookupOutcome.Routed);

        actual.Should().BeSameAs(expected);
    }

    [Fact]
    public void Closed_and_unknown_ticks_are_expected_outcomes()
    {
        var index = new ContractIdRouteIndex();
        index.RegisterKnownContract("KNOWN");

        index.TryRoute(new PositionMarketTick("KNOWN", 1m, 1, Now), out _)
            .Should().Be(MarketRouteLookupOutcome.NoOpenPosition);
        index.TryRoute(new PositionMarketTick("UNKNOWN", 1m, 1, Now), out _)
            .Should().Be(MarketRouteLookupOutcome.UnknownInstrument);
        index.TryRoute(new PositionMarketTick("", 1m, 1, Now), out _)
            .Should().Be(MarketRouteLookupOutcome.InvalidTick);
    }

    [Fact]
    public void Actors_inherit_directly_from_their_standard_framework_base()
    {
        AssertDirectBase(typeof(TradeOrderCommandActor), typeof(BaseEventSourceCommandActor<TradeOrderCommandActor>));
        AssertDirectBase(typeof(OrderExecutionCommandActor), typeof(BaseEventSourceCommandActor<OrderExecutionCommandActor>));
        AssertDirectBase(typeof(FuturesTradeCommandActor), typeof(BaseEventSourceCommandActor<FuturesTradeCommandActor>));
        AssertDirectBase(typeof(FuturesOptionTradeCommandActor), typeof(BaseEventSourceCommandActor<FuturesOptionTradeCommandActor>));
        AssertDirectBase(typeof(FuturesTradePositionCommandActor),
            typeof(BaseInMemoryEventSourceCommandActor<FuturesTradePositionCommandActor, FuturesPositionCommandState>));
        AssertDirectBase(typeof(FuturesIronCondorTradePositionCommandActor),
            typeof(BaseInMemoryEventSourceCommandActor<FuturesIronCondorTradePositionCommandActor, IronCondorPositionCommandState>));
        AssertDirectBase(typeof(FuturesVerticalSpreadTradePositionCommandActor),
            typeof(BaseInMemoryEventSourceCommandActor<FuturesVerticalSpreadTradePositionCommandActor, VerticalSpreadPositionCommandState>));
        AssertDirectBase(typeof(TradeOrderQueryActor), typeof(BaseQueryActor<TradeOrderQueryActor>));
        AssertDirectBase(typeof(OrderExecutionQueryActor), typeof(BaseQueryActor<OrderExecutionQueryActor>));
        AssertDirectBase(typeof(FuturesTradeQueryActor), typeof(BaseQueryActor<FuturesTradeQueryActor>));
        AssertDirectBase(typeof(FuturesOptionTradeQueryActor), typeof(BaseQueryActor<FuturesOptionTradeQueryActor>));
        AssertDirectBase(typeof(FuturesOptionPositionQueryActor), typeof(BaseQueryActor<FuturesOptionPositionQueryActor>));
        AssertDirectBase(typeof(FuturesPositionQueryActor), typeof(BaseQueryActor<FuturesPositionQueryActor>));
        AssertDirectBase(typeof(FuturesRealtimeActor), typeof(BaseEventActor<FuturesRealtimeActor>));
        AssertDirectBase(typeof(FuturesOptionRealtimeActor), typeof(BaseEventActor<FuturesOptionRealtimeActor>));
    }

    [Fact]
    public void Trade_domain_contains_no_catch_all_lifecycle_namespace()
    {
        typeof(TradeOrderCommandActor).Assembly.GetTypes()
            .Where(type => type.Namespace is not null)
            .Should().NotContain(type => type.Namespace!.Contains(".Lifecycle", StringComparison.Ordinal));
    }

    [Fact]
    public void Trade_domain_contains_no_legacy_option_trade_command_authority()
    {
        var tradeTypes = typeof(TradeOrderCommandActor).Assembly.GetTypes();
        tradeTypes.Should().NotContain(type =>
            type.FullName != null
            && type.FullName.StartsWith(
                "TomasAI.IFM.Domain.Trade.Option.Command.",
                StringComparison.Ordinal));

        var contractTypes = typeof(CreateTradeOrderCommand).Assembly.GetTypes();
        contractTypes.Should().NotContain(type =>
            type.Namespace == "TomasAI.IFM.Domain.Trade.Shared.Commands"
            && type.Name.Contains("OptionTrade", StringComparison.Ordinal));
    }

    [Fact]
    public void Additive_trade_flow_schema_contains_recovery_and_history_without_destructive_create_statements()
    {
        string[] creates =
        [
            TradeFlowSchemaCql.TradeOrder, TradeFlowSchemaCql.OrderExecution,
            TradeFlowSchemaCql.ExecutionFill, TradeFlowSchemaCql.EstablishedTrade,
            TradeFlowSchemaCql.EstablishedTradeHistory, TradeFlowSchemaCql.PositionCurrent,
            TradeFlowSchemaCql.PositionHistory, TradeFlowSchemaCql.OpenPositionRoute,
            TradeFlowSchemaCql.OpenPositionRouteRecovery
        ];

        creates.Should().OnlyContain(sql => sql.Contains("CREATE TABLE IF NOT EXISTS", StringComparison.Ordinal));
        creates.Should().OnlyContain(sql => !sql.Contains("DROP ", StringComparison.OrdinalIgnoreCase));
        TradeFlowSchemaCql.EstablishedTradeHistory.Should().Contain(
            "orderId, tradeId, evidenceRevision)",
            "each evidence amendment must append an immutable history row");
    }

    static void AssertDirectBase(Type actor, Type expectedBase)
    {
        actor.Should().Match(type => type.IsClass && type.IsSealed && !type.IsAbstract);
        actor.BaseType.Should().Be(expectedBase);
    }
}
