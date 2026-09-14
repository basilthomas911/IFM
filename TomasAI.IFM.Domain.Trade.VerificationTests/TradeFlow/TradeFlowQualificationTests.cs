using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Application.Storage.TradePlanDb.Schema;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.EventProjector;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.OrderComposer.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.RiskManager.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.EventProjector;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.OrderComposer.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.RiskManager.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.EventProjector;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.OrderComposer.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.RiskManager.Function.Actor;
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
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

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
            , typeof(TradePlanParameters), typeof(StrategyTradePlanSnapshot),
            typeof(TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId),
            typeof(VerticalSpreadTradePlanId), typeof(FuturesTradePlanId),
            typeof(UpdateIronCondorTradePlanCommand), typeof(UpdateVerticalSpreadTradePlanCommand),
            typeof(UpdateFuturesTradePlanCommand), typeof(IronCondorTradePlanUpdatedEvent),
            typeof(VerticalSpreadTradePlanUpdatedEvent), typeof(FuturesTradePlanUpdatedEvent),
            typeof(StrategyTradePlanActivityPage), typeof(GetStrategyTradePlanActivityQuery),
            typeof(ExitPositionWorkflowProjection), typeof(PositionExitWorkflowHistoryPage),
            typeof(GetPositionExitWorkflowQuery), typeof(GetPositionExitWorkflowTimelineQuery)
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
    public void One_hundred_thousand_position_updates_replay_deterministically_and_reject_stale_ticks()
    {
        var tradeId = new TradeEntityId(11, 22, 33, 44);
        var positionId = new StrategyPositionId(
            tradeId,
            Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var legId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var state = new StrategyPositionActorStateMachine();
        state.Replay(new StrategyPositionSnapshot
        {
            Id = positionId,
            StrategyKind = TradeStrategyKind.FuturesOutright,
            Phase = StrategyPositionPhase.Open,
            PositionSequence = 1,
            RouteGeneration = 1,
            Legs =
            [
                new StrategyPositionLeg
                {
                    TradeLegId = legId,
                    ContractId = "ESZ6",
                    ContractKey = "ESZ6",
                    AssetFamily = TradeAssetFamily.Futures,
                    SignedQuantity = 1,
                    OpeningPrice = 5_000m,
                    CurrentPrice = 5_000m,
                    LastPriceAtUtc = Now
                }
            ],
            MarketValue = 5_000m,
            AsOfUtc = Now,
            IsOpen = true
        });

        for (var sourceSequence = 1L; sourceSequence <= 100_000; sourceSequence++)
        {
            var occurredAtUtc = Now.AddTicks(sourceSequence);
            var price = 5_000m + sourceSequence / 100m;
            state.UpdateLeg(legId, "ESZ6", price, sourceSequence, occurredAtUtc, 1)
                .Accepted.Should().BeTrue();

            if (sourceSequence % 10_000 != 0)
                continue;

            state.UpdateLeg(legId, "ESZ6", price, sourceSequence, occurredAtUtc, 1)
                .Code.Should().Be("POSITION.DUPLICATE_OR_OUT_OF_ORDER");
            state.UpdateLeg(legId, "ESZ6", price, sourceSequence - 1, occurredAtUtc, 1)
                .Code.Should().Be("POSITION.DUPLICATE_OR_OUT_OF_ORDER");
        }

        var finalSnapshot = state.Current!;
        finalSnapshot.PositionSequence.Should().Be(100_001);
        finalSnapshot.Legs.Single().LastSourceSequence.Should().Be(100_000);

        var recovered = new StrategyPositionActorStateMachine();
        recovered.Replay(MessagePackSerializer.Deserialize<StrategyPositionSnapshot>(
            MessagePackSerializer.Serialize(finalSnapshot)));

        recovered.Current.Should().BeEquivalentTo(finalSnapshot);
        recovered.UpdateLeg(legId, "ESZ6", 6_001m, 100_001, Now.AddTicks(100_001), 1)
            .Accepted.Should().BeTrue();
        recovered.Current!.PositionSequence.Should().Be(100_002);
        recovered.Current.Legs.Single().LastSourceSequence.Should().Be(100_001);
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
        AssertDirectBase(typeof(IronCondorTradePlanFunctionActor),
            typeof(BaseEventSourceFunctionActor<IronCondorTradePlanFunctionActor, UpdateIronCondorTradePlanCommand,
                TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId,
                TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId,
                IronCondorTradePlanFunctionState, IronCondorTradePlanUpdatedEvent,
                TradePlanFailedEvent<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId>>));
        AssertDirectBase(typeof(VerticalSpreadTradePlanFunctionActor),
            typeof(BaseEventSourceFunctionActor<VerticalSpreadTradePlanFunctionActor, UpdateVerticalSpreadTradePlanCommand,
                VerticalSpreadTradePlanId, VerticalSpreadTradePlanId, VerticalSpreadTradePlanFunctionState,
                VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>>));
        AssertDirectBase(typeof(FuturesTradePlanFunctionActor),
            typeof(BaseEventSourceFunctionActor<FuturesTradePlanFunctionActor, UpdateFuturesTradePlanCommand,
                FuturesTradePlanId, FuturesTradePlanId, FuturesTradePlanFunctionState,
                FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>));
        AssertDirectBase(typeof(IronCondorTradePositionRealtimeActor),
            typeof(BaseEventActor<IronCondorTradePositionRealtimeActor>));
        AssertDirectBase(typeof(VerticalSpreadTradePositionRealtimeActor),
            typeof(BaseEventActor<VerticalSpreadTradePositionRealtimeActor>));
        AssertDirectBase(typeof(FuturesTradePositionRealtimeActor),
            typeof(BaseEventActor<FuturesTradePositionRealtimeActor>));
        AssertDirectBase(typeof(IronCondorExitOrderCompositionFunctionActor),
            typeof(BaseEventSourceFunctionActor<IronCondorExitOrderCompositionFunctionActor,
                ComposeExitOrderCommand, ExitPositionWorkflowId, ExitPositionWorkflowId,
                TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.State.ExitOrderCompositionFunctionState,
                ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>));
        AssertDirectBase(typeof(IronCondorPositionExitRiskFunctionActor),
            typeof(BaseEventSourceFunctionActor<IronCondorPositionExitRiskFunctionActor,
                EvaluatePositionExitRiskCommand, ExitPositionWorkflowId, ExitPositionWorkflowId,
                TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.State.PositionExitRiskFunctionState,
                PositionExitRiskCompletedEvent, ExitPositionWorkflowFailedEvent>));
        AssertDirectBase(typeof(VerticalSpreadExitOrderCompositionFunctionActor),
            typeof(BaseEventSourceFunctionActor<VerticalSpreadExitOrderCompositionFunctionActor,
                ComposeExitOrderCommand, ExitPositionWorkflowId, ExitPositionWorkflowId,
                TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.State.ExitOrderCompositionFunctionState,
                ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>));
        AssertDirectBase(typeof(VerticalSpreadPositionExitRiskFunctionActor),
            typeof(BaseEventSourceFunctionActor<VerticalSpreadPositionExitRiskFunctionActor,
                EvaluatePositionExitRiskCommand, ExitPositionWorkflowId, ExitPositionWorkflowId,
                TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.State.PositionExitRiskFunctionState,
                PositionExitRiskCompletedEvent, ExitPositionWorkflowFailedEvent>));
        AssertDirectBase(typeof(FuturesExitOrderCompositionFunctionActor),
            typeof(BaseEventSourceFunctionActor<FuturesExitOrderCompositionFunctionActor,
                ComposeExitOrderCommand, ExitPositionWorkflowId, ExitPositionWorkflowId,
                TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.State.ExitOrderCompositionFunctionState,
                ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>));
        AssertDirectBase(typeof(FuturesPositionExitRiskFunctionActor),
            typeof(BaseEventSourceFunctionActor<FuturesPositionExitRiskFunctionActor,
                EvaluatePositionExitRiskCommand, ExitPositionWorkflowId, ExitPositionWorkflowId,
                TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.State.PositionExitRiskFunctionState,
                PositionExitRiskCompletedEvent, ExitPositionWorkflowFailedEvent>));
    }

    [Fact]
    public void Additive_trade_plan_schema_has_one_immutable_history_table_per_strategy()
    {
        string[] creates =
        [
            TradePlanSchemaCql.IronCondor,
            TradePlanSchemaCql.VerticalSpread,
            TradePlanSchemaCql.Futures,
            TradePlanSchemaCql.ActivityByDate,
            TradePlanSchemaCql.ExitWorkflow
        ];

        creates.Should().OnlyContain(sql => sql.Contains("CREATE TABLE IF NOT EXISTS", StringComparison.Ordinal));
        creates.Take(3).Should().OnlyContain(sql =>
            sql.Contains("planRevision bigint", StringComparison.Ordinal) &&
            sql.Contains("CLUSTERING ORDER BY (planRevision DESC)", StringComparison.Ordinal));
        creates.Should().OnlyContain(sql => !sql.Contains("DROP ", StringComparison.OrdinalIgnoreCase));
        creates.Distinct(StringComparer.Ordinal).Should().HaveCount(5);
    }

    [Fact]
    public void Strategy_position_projectors_own_durable_plan_projection_recovery()
    {
        typeof(IronCondorPositionEventProjector).GetProperty(nameof(IronCondorPositionEventProjector.ProjectedEventTypes))
            .Should().NotBeNull();
        var source = new[]
        {
            File.ReadAllText(Path.Combine(RepositoryRoot(),
                "TomasAI.IFM.Domain.Trade/Futures/Option/Position/IronCondor/Command/EventProjector/IronCondorPositionEventProjector.cs")),
            File.ReadAllText(Path.Combine(RepositoryRoot(),
                "TomasAI.IFM.Domain.Trade/Futures/Option/Position/VerticalSpread/Command/EventProjector/VerticalSpreadPositionEventProjector.cs")),
            File.ReadAllText(Path.Combine(RepositoryRoot(),
                "TomasAI.IFM.Domain.Trade/Futures/Position/Command/EventProjector/FuturesPositionEventProjector.cs"))
        };

        source[0].Should().Contain(nameof(IronCondorTradePlanUpdatedEvent));
        source[1].Should().Contain(nameof(VerticalSpreadTradePlanUpdatedEvent));
        source[2].Should().Contain(nameof(FuturesTradePlanUpdatedEvent));
        source.Should().OnlyContain(text => text.Contains("DescribeNotification", StringComparison.Ordinal));

        AssertRequiredProjection(
            new IronCondorTradePlanUpdatedEvent { Plan = new StrategyTradePlanSnapshot { MaterialChange = true } },
            nameof(FuturesIronCondorTradePositionCommandActor),
            nameof(IronCondorPositionEventProjector));
        AssertRequiredProjection(
            new VerticalSpreadTradePlanUpdatedEvent { Plan = new StrategyTradePlanSnapshot { MaterialChange = true } },
            nameof(FuturesVerticalSpreadTradePositionCommandActor),
            nameof(VerticalSpreadPositionEventProjector));
        AssertRequiredProjection(
            new FuturesTradePlanUpdatedEvent { Plan = new StrategyTradePlanSnapshot { MaterialChange = true } },
            nameof(FuturesTradePositionCommandActor),
            nameof(FuturesPositionEventProjector));
        new IronCondorTradePlanUpdatedEvent().RequiresDurableProjection.Should().BeFalse();
        new VerticalSpreadTradePlanUpdatedEvent().RequiresDurableProjection.Should().BeFalse();
        new FuturesTradePlanUpdatedEvent().RequiresDurableProjection.Should().BeFalse();
    }

    static void AssertRequiredProjection(
        IRequireDurableProjection sourceEvent,
        string actorName,
        string projectorName)
    {
        sourceEvent.RequiresDurableProjection.Should().BeTrue();
        sourceEvent.RequiredProjection.ActorName.Should().Be(actorName);
        sourceEvent.RequiredProjection.ProjectorName.Should().Be(projectorName);
        sourceEvent.RequiredProjection.InitialStage.Should().Be(EventProjectorStageType.ApplyProjection);
    }

    [Fact]
    public void Trade_domain_contains_no_catch_all_lifecycle_namespace()
    {
        typeof(TradeOrderCommandActor).Assembly.GetTypes()
            .Where(type => type.Namespace is not null)
            .Should().NotContain(type => type.Namespace!.Contains(".Lifecycle", StringComparison.Ordinal));
    }

    [Fact]
    public void Trade_domain_contains_no_redundant_nested_trade_hierarchy()
    {
        typeof(TradeOrderCommandActor).Assembly.GetTypes()
            .Where(type => type.Namespace is not null)
            .Should().NotContain(type => type.Namespace!.StartsWith(
                "TomasAI.IFM.Domain.Trade.Trade.", StringComparison.Ordinal));

        Directory.Exists(Path.Combine(
                RepositoryRoot(),
                "TomasAI.IFM.Domain.Trade",
                "Trade"))
            .Should().BeFalse();
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

    static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
