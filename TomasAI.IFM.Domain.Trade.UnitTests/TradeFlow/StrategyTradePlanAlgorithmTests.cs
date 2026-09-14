using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Model;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Model;
using TomasAI.IFM.Domain.Trade.Futures.Position.Realtime;
using TomasAI.IFM.Domain.Trade.Position.Workflow.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using PositionIronCondorTradePlanId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class StrategyTradePlanAlgorithmTests
{
    static readonly DateTime Now = new(2026, 9, 14, 14, 30, 0, DateTimeKind.Utc);
    static readonly DateOnly ValueDate = DateOnly.FromDateTime(Now);

    [Fact]
    public void Iron_condor_first_update_creates_a_material_normal_plan()
    {
        var plan = new IronCondorTradePlanAlgorithm().Calculate(
            Position(TradeStrategyKind.IronCondor, 4, unrealizedPnl: 25m),
            Parameters(), null, ValueDate, Now).Snapshot;

        plan.PlanRevision.Should().Be(1);
        plan.State.Should().Be(TradePlanState.Normal);
        plan.Action.Should().Be(TradePlanAction.Monitor);
        plan.RequiresExit.Should().BeFalse();
        plan.MaterialChange.Should().BeTrue();
        plan.ContentHash.Should().HaveLength(64);
    }

    [Fact]
    public void Iron_condor_maximum_loss_requires_an_immediate_market_exit()
    {
        var plan = new IronCondorTradePlanAlgorithm().Calculate(
            Position(TradeStrategyKind.IronCondor, 4, unrealizedPnl: -1_100m),
            Parameters(), null, ValueDate, Now).Snapshot;

        plan.State.Should().Be(TradePlanState.ExitRequired);
        plan.Action.Should().Be(TradePlanAction.ExitAtMarket);
        plan.RequiresExit.Should().BeTrue();
        plan.ReasonCode.Should().Be("PLAN.MAX_LOSS");
    }

    [Fact]
    public void Iron_condor_profit_target_requires_a_limit_exit()
    {
        var plan = new IronCondorTradePlanAlgorithm().Calculate(
            Position(TradeStrategyKind.IronCondor, 4, unrealizedPnl: 550m),
            Parameters(), null, ValueDate, Now).Snapshot;

        plan.State.Should().Be(TradePlanState.ExitRequired);
        plan.Action.Should().Be(TradePlanAction.ExitAtLimit);
        plan.ReasonCode.Should().Be("PLAN.PROFIT_TARGET");
    }

    [Fact]
    public void Stale_market_data_places_the_plan_on_hold_without_exiting()
    {
        var position = Position(TradeStrategyKind.IronCondor, 4, unrealizedPnl: -1_100m) with
        {
            AsOfUtc = Now.AddMinutes(-1)
        };

        var plan = new IronCondorTradePlanAlgorithm().Calculate(
            position, Parameters(), null, ValueDate, Now).Snapshot;

        plan.State.Should().Be(TradePlanState.Hold);
        plan.Action.Should().Be(TradePlanAction.Hold);
        plan.RequiresExit.Should().BeFalse();
        plan.ReasonCode.Should().Be("PLAN.DATA.STALE");
    }

    [Fact]
    public void A_small_unchanged_update_is_persisted_but_not_materially_projected()
    {
        var algorithm = new IronCondorTradePlanAlgorithm();
        var first = algorithm.Calculate(Position(TradeStrategyKind.IronCondor, 4, unrealizedPnl: 25m),
            Parameters(), null, ValueDate, Now).Snapshot;
        var secondPosition = Position(TradeStrategyKind.IronCondor, 4, unrealizedPnl: 26m, sequence: 2);

        var second = algorithm.Calculate(secondPosition, Parameters(), first, ValueDate, Now.AddSeconds(1)).Snapshot;

        second.PlanRevision.Should().Be(2);
        second.MaterialChange.Should().BeFalse();
    }

    [Fact]
    public void Vertical_spread_and_futures_enforce_their_strategy_topologies()
    {
        var spread = new VerticalSpreadTradePlanAlgorithm().Calculate(
            Position(TradeStrategyKind.VerticalSpread, 2, unrealizedPnl: 10m),
            Parameters(), null, ValueDate, Now).Snapshot;
        var future = new FuturesTradePlanAlgorithm().Calculate(
            Position(TradeStrategyKind.FuturesOutright, 1, unrealizedPnl: 10m),
            Parameters(), null, ValueDate, Now).Snapshot;

        spread.State.Should().Be(TradePlanState.Normal);
        future.State.Should().Be(TradePlanState.Normal);
        FluentActions.Invoking(() => new VerticalSpreadTradePlanAlgorithm().Calculate(
                Position(TradeStrategyKind.VerticalSpread, 1), Parameters(), null, ValueDate, Now))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new FuturesTradePlanAlgorithm().Calculate(
                Position(TradeStrategyKind.FuturesOutright, 2), Parameters(), null, ValueDate, Now))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Stable_stream_state_replays_a_duplicate_and_exposes_the_previous_plan_to_a_later_command()
    {
        var position = Position(TradeStrategyKind.IronCondor, 4);
        var entityId = new PositionIronCondorTradePlanId(position.Id, ValueDate);
        var original = Command(entityId, position, Guid.NewGuid());
        var snapshot = new IronCondorTradePlanAlgorithm().Calculate(
            position, original.Parameters, null, ValueDate, Now).Snapshot;
        var completed = new IronCondorTradePlanUpdatedEvent
        {
            Subject = original.Subject,
            Id = Guid.NewGuid(),
            EventId = 42,
            EntityId = entityId,
            CommandId = original.CommandId,
            AggregateId = original.StreamId,
            ReceivedOn = Now,
            Plan = snapshot,
            RequestFingerprint = original.Fingerprint(),
            SourceEventId = original.SourceEventId
        };
        var state = new IronCondorTradePlanFunctionState().Prepare(original);

        state.TryComplete(completed, original).Should().BeTrue();
        state.IsCompleted.Should().BeTrue();
        state.Matches(original).Should().BeTrue();
        state.LastPersistedEventId.Should().Be(42);

        var later = Command(entityId, position with { PositionSequence = 2 }, Guid.NewGuid());
        state.Prepare(later);

        state.IsCompleted.Should().BeFalse();
        state.CompletedEvent.Should().BeNull();
        state.PreviousPlan.Should().BeSameAs(snapshot);
    }

    [Fact]
    public void Invalid_parameters_and_position_shape_fail_before_a_plan_is_created()
    {
        var invalidParameters = Parameters() with { WarningLoss = 1_500m };
        FluentActions.Invoking(() => new IronCondorTradePlanAlgorithm().Calculate(
                Position(TradeStrategyKind.IronCondor, 4), invalidParameters, null, ValueDate, Now))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new IronCondorTradePlanAlgorithm().Calculate(
                Position(TradeStrategyKind.IronCondor, 3), Parameters(), null, ValueDate, Now))
            .Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(TradeStrategyKind.IronCondor, 4)]
    [InlineData(TradeStrategyKind.VerticalSpread, 2)]
    [InlineData(TradeStrategyKind.FuturesOutright, 1)]
    public void Exit_composition_exactly_reverses_every_remaining_leg_and_marks_the_order_as_closing(
        TradeStrategyKind strategy, int legCount)
    {
        var position = Position(strategy, legCount, -1_100m);
        var workflowId = new ExitPositionWorkflowId(position.Id, ValueDate, Guid.NewGuid());
        var started = new ExitPositionWorkflowStartedEvent
        {
            Id = Guid.NewGuid(),
            EntityId = workflowId,
            StrategyKind = strategy,
            SourcePlanEventId = Guid.NewGuid(),
            ExitPlan = new StrategyTradePlanSnapshot
            {
                Position = position,
                ValueDate = ValueDate,
                State = TradePlanState.ExitRequired,
                Action = TradePlanAction.ExitAtMarket,
                RequiresExit = true
            }
        };

        var composition = StrategyExitOrderCompositionModel.Compose(started, Now);

        composition.PositionType.Should().Be(TradeOrderPositionType.Closing);
        composition.Component.ReservedTradeId.Should().Be(position.Id.Trade.TradeId);
        composition.Component.Legs.Should().HaveCount(legCount);
        composition.Component.Legs.Should().OnlyContain(close => position.Legs.Any(open =>
            open.TradeLegId == close.TradeLegId && open.ContractId == close.ContractId &&
            close.SignedQuantity == -open.SignedQuantity));
        composition.CompositionHash.Should().HaveLength(64);
    }

    [Fact]
    public void Exit_composition_rejects_a_plan_that_does_not_require_exit()
    {
        var position = Position(TradeStrategyKind.FuturesOutright, 1);
        var started = new ExitPositionWorkflowStartedEvent
        {
            EntityId = new(position.Id, ValueDate, Guid.NewGuid()),
            StrategyKind = position.StrategyKind,
            SourcePlanEventId = Guid.NewGuid(),
            ExitPlan = new StrategyTradePlanSnapshot
            {
                Position = position,
                ValueDate = ValueDate,
                State = TradePlanState.Normal,
                Action = TradePlanAction.Monitor
            }
        };

        FluentActions.Invoking(() => StrategyExitOrderCompositionModel.Compose(started, Now))
            .Should().Throw<InvalidOperationException>().WithMessage("EXIT.COMPOSITION.INVALID_PLAN");
    }

    static TradePlanParameters Parameters() => new()
    {
        Version = 3,
        MaximumLoss = 1_000m,
        WarningLoss = 750m,
        ProfitTarget = 500m,
        MaterialPnlChange = 10m,
        MaterialPriceChange = 0.25m,
        MaximumDataAgeSeconds = 30
    };

    static UpdateIronCondorTradePlanCommand Command(
        PositionIronCondorTradePlanId entityId, StrategyPositionSnapshot position, Guid commandId) => new()
    {
        CommandId = commandId,
        EntityId = entityId,
        Subject = new ActorSubject(ActorType.Function, UpdateIronCondorTradePlanCommand.Actor,
            UpdateIronCondorTradePlanCommand.Verb, entityId.Format()),
        Position = position,
        Parameters = Parameters(),
        SourceEventId = Guid.NewGuid(),
        RequestedAtUtc = Now
    };

    static StrategyPositionSnapshot Position(
        TradeStrategyKind strategy, int legCount, decimal unrealizedPnl = 0m, long sequence = 1)
    {
        var id = new StrategyPositionId(new TradeEntityId(1, 2, 3, 4),
            Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var legs = Enumerable.Range(1, legCount).Select(index => new StrategyPositionLeg
        {
            TradeLegId = Guid.Parse($"00000000-0000-0000-0000-{index:D12}"),
            SignedQuantity = index % 2 == 0 ? -1 : 1,
            OpeningPrice = 10m + index,
            CurrentPrice = 10.1m + index,
            LastSourceSequence = sequence,
            LastPriceAtUtc = Now,
            ContractId = $"CONTRACT-{index}",
            AssetFamily = strategy == TradeStrategyKind.FuturesOutright
                ? TradeAssetFamily.Futures
                : TradeAssetFamily.FuturesOption
        }).ToArray();
        return new StrategyPositionSnapshot
        {
            Id = id,
            StrategyKind = strategy,
            Phase = StrategyPositionPhase.MarkToMarket,
            PositionSequence = sequence,
            RouteGeneration = 1,
            Legs = legs,
            MarketValue = legs.Sum(leg => leg.CurrentPrice * leg.SignedQuantity),
            UnrealizedPnl = unrealizedPnl,
            AsOfUtc = Now,
            IsOpen = true
        };
    }
}
