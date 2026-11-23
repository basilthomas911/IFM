using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;
using MessagePack;
using System.Buffers;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Model;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Model;
using TomasAI.IFM.Domain.Trade.Futures.Position.Realtime;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;
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
    public void Single_snapshot_state_recognizes_its_command_and_carries_only_the_latest_revision_to_a_later_request()
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

        var later = Command(entityId, position with { PositionSequence = 2 }, Guid.NewGuid());
        state.Prepare(later);

        state.IsCompleted.Should().BeFalse();
        state.CompletedEvent.Should().BeNull();
        state.LatestPlanRevision.Should().Be(snapshot.PlanRevision);
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

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void Futures_trail_is_ten_price_points_and_survives_snapshot_restore(int direction)
    {
        var algorithm = new FuturesTradePlanAlgorithm();
        var entry = FuturePosition(5000m, direction);
        var initial = algorithm.Calculate(entry, Parameters(), null, ValueDate, Now).Snapshot;
        initial.FuturesTrailingExtremePrice.Should().Be(5000m);
        initial.RequiresExit.Should().BeFalse();
        var peakPosition = entry with { PositionSequence = 2, Legs = [entry.Legs[0] with { CurrentPrice = 5000m + direction * 20m }] };
        var peak = algorithm.Calculate(peakPosition, Parameters(), initial, ValueDate, Now).Snapshot;
        var restored = MessagePackSerializer.Deserialize<StrategyTradePlanSnapshot>(MessagePackSerializer.Serialize(peak));
        restored.ContentHash.Should().Be(TradePlanContractIdentity.PlanHash(restored));
        restored.FuturesTrailingExtremePrice.Should().Be(5000m + direction * 20m);
        var nearStop = entry with { PositionSequence = 3, Legs = [entry.Legs[0] with { CurrentPrice = 5000m + direction * 10.25m }] };
        var held = algorithm.Calculate(nearStop, Parameters(), restored, ValueDate, Now).Snapshot;
        held.RequiresExit.Should().BeFalse();
        held.FuturesTrailingExtremePrice.Should().Be(restored.FuturesTrailingExtremePrice);
        var atStop = nearStop with { PositionSequence = 4, Legs = [nearStop.Legs[0] with { CurrentPrice = 5000m + direction * 10m }] };
        var exit = algorithm.Calculate(atStop, Parameters(), held, ValueDate, Now).Snapshot;
        exit.RequiresExit.Should().BeTrue();
        exit.Action.Should().Be(TradePlanAction.ExitAtMarket);
        exit.ReasonCode.Should().Be("PLAN.FUTURES.TRAILING_STOP");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void Futures_trail_protects_the_entry_immediately_and_uses_prices_not_cash_pnl(int direction)
    {
        var position = FuturePosition(5000m - direction * 10m, direction);
        position.UnrealizedPnl.Should().Be(0);
        var plan = new FuturesTradePlanAlgorithm().Calculate(position, Parameters(), null, ValueDate, Now).Snapshot;
        plan.RequiresExit.Should().BeTrue();
        plan.ReasonCode.Should().Be("PLAN.FUTURES.TRAILING_STOP");
    }

    [Fact]
    public void Futures_stale_prices_do_not_advance_or_trigger_the_trail()
    {
        var entry = FuturePosition(5000m, 1);
        var algorithm = new FuturesTradePlanAlgorithm();
        var initial = algorithm.Calculate(entry, Parameters(), null, ValueDate, Now).Snapshot;
        var staleHigh = entry with { Legs = [entry.Legs[0] with { CurrentPrice = 5100m, LastPriceAtUtc = Now.AddMinutes(-1) }] };
        var plan = algorithm.Calculate(staleHigh, Parameters(), initial, ValueDate, Now).Snapshot;
        plan.FuturesTrailingExtremePrice.Should().Be(5000m);
        var staleLow = staleHigh with { Legs = [staleHigh.Legs[0] with { CurrentPrice = 4900m }] };
        algorithm.Calculate(staleLow, Parameters(), plan, ValueDate, Now).Snapshot.RequiresExit.Should().BeFalse();
    }

    [Fact]
    public void Futures_watermark_changes_are_material_and_part_of_the_plan_hash()
    {
        var position = FuturePosition(5000m, 1);
        var plan = new FuturesTradePlanAlgorithm().Calculate(position, Parameters(), null, ValueDate, Now).Snapshot;
        var changed = plan with { FuturesTrailingExtremePrice = 5010m };
        TradePlanContractIdentity.PlanHash(changed).Should().NotBe(plan.ContentHash);
        FuturesTradePlanComparisonModel.IsMaterial(plan, changed).Should().BeTrue();
    }

    [Fact]
    public void Futures_legacy_snapshots_without_a_watermark_seed_from_the_fill_and_new_observation()
    {
        var position = FuturePosition(5000m, 1);
        var algorithm = new FuturesTradePlanAlgorithm();
        var legacy = algorithm.Calculate(position, Parameters(), null, ValueDate, Now).Snapshot with { FuturesTrailingExtremePrice = null };
        legacy = legacy with { ContentHash = TradePlanContractIdentity.PlanHash(legacy) };
        var reader = new MessagePackReader(MessagePackSerializer.Serialize(legacy));
        reader.ReadArrayHeader().Should().Be(23);
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(20);
        for (var field = 0; field < 20; field++)
            writer.WriteRaw(reader.ReadRaw().ToArray());
        writer.Flush();
        legacy = MessagePackSerializer.Deserialize<StrategyTradePlanSnapshot>(buffer.WrittenMemory);
        legacy.FuturesTrailingExtremePrice.Should().BeNull();
        legacy.ContentHash.Should().Be(TradePlanContractIdentity.PlanHash(legacy));
        var updated = position with { PositionSequence = 2, Legs = [position.Legs[0] with { CurrentPrice = 5015m }] };
        var plan = algorithm.Calculate(updated, Parameters(), legacy, ValueDate, Now).Snapshot;
        plan.FuturesTrailingExtremePrice.Should().Be(5015m);
        plan.RequiresExit.Should().BeFalse();
    }

    static StrategyPositionSnapshot FuturePosition(decimal currentPrice, int direction)
    {
        var position = Position(TradeStrategyKind.FuturesOutright, 1);
        return position with { Legs = [position.Legs[0] with { OpeningPrice = 5000m, CurrentPrice = currentPrice, SignedQuantity = direction * 3 }] };
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
