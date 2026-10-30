using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Risk;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class IronCondorDailyRiskSnapshotTests
{
    static readonly DateTime At = new(2026, 10, 9, 15, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void Qualified_option_calculator_populates_broker_currency_and_all_risk_metrics(int quantity)
    {
        var (plan, inputs) = Capture(quantity);
        var snapshot = new IronCondorTradePlanSnapshotCalculator().Create(plan, Guid.NewGuid(), plan.ValueDate, plan.ValueDate.AddDays(24), inputs);
        snapshot.IsComplete.Should().BeTrue(string.Join(";", snapshot.UnavailableReasons));
        snapshot.Currency.Should().Be("USD");
        snapshot.DailyPnl.Should().Be(-400 * quantity);
        snapshot.Quantity.Should().Be(quantity);
        snapshot.ContractMultiplier.Should().Be(50);
        snapshot.NetDelta.Should().NotBeNull();
        snapshot.NetGamma.Should().NotBeNull();
        snapshot.NetVega.Should().NotBeNull();
        snapshot.NetTheta.Should().NotBeNull();
        snapshot.ForwardDelta.Should().NotBeNull();
        snapshot.ScenarioUnderlyingMove.Should().BeOneOf(-10m, 10m);
        snapshot.EstimatedExitCosts.Should().BeGreaterThan(0);
        snapshot.ForwardLossRatio.Should().Be((double)(snapshot.ForwardLoss!.Value / 1000));
        snapshot.ForwardDailyPnl.Should().Be(snapshot.DailyPnl + snapshot.ProjectedPositionValueChange - snapshot.EstimatedExitCosts);
        snapshot.Legs.Should().HaveCount(4).And.OnlyContain(leg => leg.TheoreticalPrice >= 0 && leg.ForwardPrice >= 0);
        var restored = MessagePackSerializer.Deserialize<IronCondorTradePlanSnapshot>(MessagePackSerializer.Serialize(snapshot));
        restored.Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public void New_position_marks_rebind_daily_pnl_and_leg_marks_without_reusing_old_pnl()
    {
        var (plan, inputs) = Capture();
        var position = plan.Position with { PositionSequence = 2, DailyPnlPoints = -12,
            Legs = plan.Position.Legs.Select(leg => leg with { CurrentPrice = 12 }).ToArray() };
        var snapshot = new IronCondorTradePlanSnapshotCalculator().Create(plan with { Position = position }, Guid.NewGuid(), plan.ValueDate, null, inputs);
        snapshot.IsComplete.Should().BeTrue();
        snapshot.DailyPnl.Should().Be(-600);
        snapshot.Legs.Should().OnlyContain(leg => leg.MarkPrice == 12);
    }

    [Fact]
    public void Stale_or_foreign_generation_inputs_never_produce_an_exit_ratio()
    {
        var (plan, inputs) = Capture();
        var calculator = new IronCondorTradePlanSnapshotCalculator();
        var stale = calculator.Create(plan with { CalculatedAtUtc = At.AddMinutes(1) }, Guid.NewGuid(), null, null, inputs);
        stale.CalculationStatus.Should().Be("Stale");
        stale.ForwardLossRatio.Should().BeNull();
        stale.ExitRecommended.Should().BeNull();
        var foreign = calculator.Create(plan, Guid.NewGuid(), null, null, inputs with { RouteGeneration = 2 });
        foreign.IsComplete.Should().BeFalse();
        foreign.ForwardLossRatio.Should().BeNull();
    }

    [Fact]
    public void Loss_trigger_uses_forward_currency_loss_and_never_exits_on_positive_daily_pnl()
    {
        var (plan, inputs) = Capture();
        var policy = inputs.StrategyRiskParameterSet! with { IronCondor = inputs.StrategyRiskParameterSet!.IronCondor! with { DailyLossLimit = 450 } };
        inputs = inputs with { StrategyRiskParameterSet = policy, DailyLossLimit = 450,
            Legs = inputs.Legs.Select(leg => leg with { ForwardPrice = leg.TheoreticalPrice + (leg.SignedQuantity < 0 ? 4 : 0) }).ToArray() };
        var calculator = new IronCondorTradePlanSnapshotCalculator();
        var snapshot = calculator.Create(plan, Guid.NewGuid(), null, null, inputs);
        snapshot.DailyLoss.Should().BeLessThan(snapshot.DailyLossLimit!.Value);
        snapshot.ForwardLossRatio.Should().BeGreaterThan(1);
        snapshot.ExitRecommended.Should().BeTrue();
        var profit = calculator.Create(plan with { Position = plan.Position with { DailyPnlPoints = 1 } }, Guid.NewGuid(), null, null, inputs);
        profit.ForwardLossRatio.Should().BeGreaterThan(0);
        profit.ExitRecommended.Should().BeFalse();
    }

    [Fact]
    public void Missing_contract_metadata_does_not_invent_currency_pnl_or_forward_loss()
    {
        var (plan, _) = Capture();
        var inputs = IronCondorTradePlanSnapshotCalculator.CaptureUnavailableInputs(plan.Position,
            new() { ContractCashMultiplier = 50, OpeningCommission = 20, TradeDate = plan.ValueDate });
        var snapshot = new IronCondorTradePlanSnapshotCalculator().Create(plan, Guid.NewGuid(), plan.ValueDate, null, inputs);
        snapshot.DailyPnl.Should().BeNull();
        snapshot.ForwardLossRatio.Should().BeNull();
        snapshot.ExitRecommended.Should().BeNull();
    }

    [Fact]
    public void Consecutive_qualified_underlying_ticks_are_repriced_against_one_newest_quote()
    {
        var (plan, _) = Capture();
        var (legs, risk) = IronCondorOptionCalculatorTests.Evidence(atUtc: new DateTimeOffset(At));
        var position = plan.Position with { Legs = legs.Select(leg => new StrategyPositionLeg {
            TradeLegId = leg.TradeLegId, ContractId = leg.ContractId, SignedQuantity = leg.SignedQuantity, CurrentPrice = 10 }).ToArray() };
        for (var i = 0; i < risk.Length; i++)
        {
            var item = risk[i].Instruments.Single();
            var quote = item.Instrument.Underlying! with { EventAtUtc = new DateTimeOffset(At).AddMilliseconds(i == 0 ? 0 : -100),
                Bid = i == 0 ? 5000.25m : 4999.75m, Ask = i == 0 ? 5000.75m : 5000.25m };
            risk[i] = risk[i] with { Instruments = [item with { Instrument = item.Instrument with { Underlying = quote } }] };
        }
        var trade = new EstablishedTradeDefinition { Id = position.Id.Trade, Legs = legs, EstablishedAtUtc = At };
        var inputs = new IronCondorTradePlanSnapshotCalculator().CaptureScenarioInputs(trade, position, risk,
            StrategyRiskParameterSet.CreateIronCondorDevelopmentDefault(), At);
        inputs.UnderlyingPrice.Should().Be(5000.5m);
        inputs.ScenarioUnderlyingPrice.Should().BeOneOf(4990.5m, 5010.5m);
    }

    [Fact]
    public void Retained_legs_calculate_full_snapshot_without_scope_capture_or_synchronized_ticks()
    {
        var (plan, inputs) = Retained();
        var snapshot = new IronCondorTradePlanSnapshotCalculator().Create(plan, Guid.NewGuid(), plan.ValueDate, null,
            inputs with { Legs = [], ScenarioUnderlyingPrice = null, EstimatedExitSlippage = null });
        snapshot.IsComplete.Should().BeTrue(string.Join(";", snapshot.UnavailableReasons));
        snapshot.Position.Legs.Should().HaveCount(4).And.OnlyContain(leg => leg.OptionRisk != null && leg.OptionTick != null);
        snapshot.Quantity.Should().Be(1);
        snapshot.CurrentPriceBasis.Should().Be("PositionMark");
        snapshot.NetDelta.Should().NotBeNull(); snapshot.NetGamma.Should().NotBeNull();
        snapshot.NetVega.Should().NotBeNull(); snapshot.NetTheta.Should().NotBeNull();
        snapshot.ForwardLossRatio.Should().NotBeNull(); snapshot.EstimatedExitCosts.Should().NotBeNull();
        snapshot.PutOTMProbability.Should().NotBeNull(); snapshot.CallOTMProbability.Should().NotBeNull();
        var json = System.Text.Json.JsonSerializer.SerializeToElement(snapshot);
        json.TryGetProperty("Legs", out _).Should().BeFalse();
        json.GetProperty("Position").GetProperty("Legs").GetArrayLength().Should().Be(4);
        var restored = MessagePackSerializer.Deserialize<IronCondorTradePlanSnapshot>(MessagePackSerializer.Serialize(snapshot));
        restored.Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public void Four_opening_marks_are_enough_for_current_spreads_without_any_risk_calculation()
    {
        var (plan, inputs) = Retained();
        plan = plan with { Position = plan.Position with { Legs = plan.Position.Legs.Select(leg => leg with
        { OptionRisk = null, Scenario = null, OptionTick = null }).ToArray() } };
        var unavailable = IronCondorTradePlanSnapshotCalculator.CaptureUnavailableInputs(plan.Position,
            new() { ContractCashMultiplier = 50, StrategyRiskParameterSet = inputs.StrategyRiskParameterSet });
        var snapshot = new IronCondorTradePlanSnapshotCalculator().Create(plan, Guid.NewGuid(), null, null, unavailable);
        snapshot.Quantity.Should().Be(1);
        snapshot.CombinedSpreadPrice.Should().Be(0);
        snapshot.CurrentPositionValue.Should().BeNull();
        snapshot.NetDelta.Should().BeNull(); snapshot.ForwardLossRatio.Should().BeNull();
        snapshot.ActionSubType.Should().BeNull();
        snapshot.Position.Legs.Should().HaveCount(4);
    }

    [Fact]
    public void Legacy_wire_leg_slot_is_migrated_into_position_legs()
    {
        var (plan, inputs) = Retained();
        var old = new LegacySnapshot { Position = plan.Position with { Legs = plan.Position.Legs.Select(leg => leg with
            { OptionTick = null, OptionRisk = null, Scenario = null }).ToArray() }, Legs = inputs.Legs };
        var migrated = MessagePackSerializer.Deserialize<IronCondorTradePlanSnapshot>(MessagePackSerializer.Serialize(old));
        migrated.Position.Legs.Should().OnlyContain(leg => leg.Scenario != null);
        migrated.Legs.Should().HaveCount(4);
        migrated.LegacyLegs.Should().BeEmpty();
    }

    [MessagePackObject]
    public sealed record LegacySnapshot
    {
        [Key(48)] public StrategyPositionSnapshot Position { get; init; } = new();
        [Key(49)] public Guid SourceEventId { get; init; }
        [Key(51)] public string[] UnavailableReasons { get; init; } = [];
        [Key(52)] public string CalculationVersion { get; init; } = "LegacyTest";
        [Key(96)] public IronCondorTradePlanLegObservation[] Legs { get; init; } = [];
    }

    [Fact]
    public void Position_retains_valid_risk_across_dropped_ticks_failed_pricing_and_restart()
    {
        var (plan, _) = Retained();
        var machine = new TomasAI.IFM.Domain.Trade.Futures.Position.Model.StrategyPositionActorStateMachine();
        machine.Replay(plan.Position);
        var leg = machine.Current!.Legs[0];
        var priorRisk = leg.OptionRisk;
        var changed = leg.OptionTick! with { TickId = 100, ObservationRevision = 2,
            ObservedAtUtc = new(At.AddSeconds(1)), GreeksAvailable = false, PricingFailure = "CalculationPending", OptionPrice = 11 };
        var result = machine.UpdateLeg(leg.TradeLegId, leg.ContractId, 11, 100, At.AddSeconds(1), 1, changed);
        result.Accepted.Should().BeTrue();
        result.Value!.Legs.Should().HaveCount(4);
        result.Value.Legs[0].OptionRisk.Should().Be(priorRisk);
        result.Value.Legs[0].CurrentPrice.Should().Be(11);
        var completed = changed with { IsPricingUpdate = true, ObservationRevision = 3,
            GreeksAvailable = true, PricingFailure = null, CalculatedAtUtc = new(At.AddSeconds(2)) };
        var priced = machine.UpdateLeg(leg.TradeLegId, leg.ContractId, 11, 100, At.AddSeconds(2), 1, completed);
        priced.Accepted.Should().BeTrue();
        priced.Value!.Legs[0].LastSourceSequence.Should().Be(100);
        priced.Value.Legs[0].LastPriceAtUtc.Should().Be(At.AddSeconds(1));
        priced.Value.Legs[0].OptionRisk.Should().Be(completed);
        var replayed = new TomasAI.IFM.Domain.Trade.Futures.Position.Model.StrategyPositionActorStateMachine();
        replayed.Replay(MessagePackSerializer.Deserialize<StrategyPositionSnapshot>(MessagePackSerializer.Serialize(priced.Value)));
        replayed.Current.Should().BeEquivalentTo(priced.Value);
        replayed.UpdateLeg(leg.TradeLegId, leg.ContractId, 10, 99, At.AddSeconds(3), 1,
            changed with { TickId = 99 }).Accepted.Should().BeFalse();
        replayed.UpdateLeg(leg.TradeLegId, leg.ContractId, 10, 101, At.AddSeconds(3), 1,
            changed with { TickId = 101, OptionPrice = double.NaN }).Accepted.Should().BeFalse();
        replayed.Current!.Legs[0].OptionRisk.Should().BeEquivalentTo(completed);
        replayed.Current.Legs[1].Should().BeEquivalentTo(plan.Position.Legs[1]);
    }

    [Fact]
    public void Pricing_completion_can_seed_a_leg_if_the_initial_quote_notification_was_dropped()
    {
        var (plan, _) = Retained();
        var original = plan.Position.Legs[0];
        var machine = new TomasAI.IFM.Domain.Trade.Futures.Position.Model.StrategyPositionActorStateMachine();
        machine.Replay(plan.Position with { Legs = plan.Position.Legs.Select(leg => leg with { OptionTick = null, OptionRisk = null }).ToArray() });
        var completed = original.OptionTick! with { TickId = 50, ObservationRevision = 2, IsPricingUpdate = true,
            ObservedAtUtc = new(At.AddSeconds(1)), CalculatedAtUtc = new(At.AddSeconds(2)) };
        var updated = machine.UpdateLeg(original.TradeLegId, original.ContractId, original.CurrentPrice, 50, At.AddSeconds(2), 1, completed);
        updated.Accepted.Should().BeTrue();
        updated.Value!.Legs[0].OptionRisk.Should().Be(completed);
        updated.Value.Legs.Should().HaveCount(4);
    }

    [Fact]
    public void Valid_quotes_calculate_close_costs_even_while_all_risk_calculations_are_pending()
    {
        var (plan, inputs) = Retained();
        plan = plan with { Position = plan.Position with { Legs = plan.Position.Legs.Select(leg => leg with
            { OptionRisk = null, Scenario = null, OptionTick = leg.OptionTick! with { GreeksAvailable = false, PricingFailure = "CalculationPending" } }).ToArray() } };
        var snapshot = new IronCondorTradePlanSnapshotCalculator().Create(plan, Guid.NewGuid(), null, null, inputs with { Legs = [] });
        snapshot.EstimatedCloseValue.Should().NotBeNull();
        snapshot.EstimatedExitCommission.Should().Be(20);
        snapshot.EstimatedExitSlippage.Should().NotBeNull();
        snapshot.EstimatedExitCosts.Should().NotBeNull();
        snapshot.NetDelta.Should().BeNull();
        snapshot.ForwardLossRatio.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Invalid_contract_multiplier_never_uses_saved_emulator_or_captured_multiplier(int multiplier)
    {
        var (plan, inputs) = Retained();
        var leg = plan.Position.Legs[0];
        leg = leg with { CashMultiplier = 1, OptionTick = leg.OptionTick! with
            { PricingInputs = leg.OptionTick.PricingInputs! with { Multiplier = multiplier } } };
        plan = plan with { Position = plan.Position with { Legs = [leg, .. plan.Position.Legs.Skip(1)] } };
        var result = new IronCondorTradePlanSnapshotCalculator().Create(plan, Guid.NewGuid(), null, null,
            inputs with { ContractMultiplier = 1 });
        result.ContractMultiplier.Should().BeNull();
        result.CurrentPositionValue.Should().BeNull();
        result.DailyPnl.Should().BeNull();
        result.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void Enriched_contract_multiplier_overrides_emulator_basis_and_leaves_commissions_unscaled()
    {
        var (plan, inputs) = Retained();
        plan = plan with { Position = plan.Position with { Legs = plan.Position.Legs.Select(leg => leg with { CashMultiplier = 1 }).ToArray() } };
        var result = new IronCondorTradePlanSnapshotCalculator().Create(plan, Guid.NewGuid(), null, null,
            inputs with { ContractMultiplier = 1, ActualDailyCommission = 2.60m });
        result.ContractMultiplier.Should().Be(50);
        result.CurrentPositionValue.Should().Be(plan.Position.Legs.Sum(leg => leg.CurrentPrice * leg.SignedQuantity * 50));
        result.DailyPnl.Should().Be(plan.Position.DailyPnlPoints * 50 - 2.60m);
    }

    [Fact]
    public void Forward_spread_values_include_quantity_and_contract_multiplier_exactly_once()
    {
        var (plan, inputs) = Retained();
        plan = plan with { Position = plan.Position with { Legs = plan.Position.Legs.Select(leg => leg with { SignedQuantity = leg.SignedQuantity * 2 }).ToArray() } };
        var result = new IronCondorTradePlanSnapshotCalculator().Create(plan, Guid.NewGuid(), null, null, inputs);
        result.PutForwardPrice.Should().Be(result.Position.Legs.Where(leg => leg.PutCall == 2).Sum(leg => leg.Scenario!.ForwardPrice * leg.SignedQuantity * 50));
        result.CallForwardPrice.Should().Be(result.Position.Legs.Where(leg => leg.PutCall == 1).Sum(leg => leg.Scenario!.ForwardPrice * leg.SignedQuantity * 50));
        result.ForwardPositionValue.Should().Be(result.PutForwardPrice + result.CallForwardPrice);
        result.CombinedForwardPrice.Should().Be(result.ForwardPositionValue / (2 * 50));
    }

    static (StrategyTradePlanSnapshot Plan, IronCondorDailyRiskInputs Inputs) Retained()
    {
        var (plan, inputs) = Capture();
        var generation = Guid.NewGuid();
        var legs = plan.Position.Legs.Select((leg, index) =>
        {
            var source = inputs.Legs.Single(item => item.TradeLegId == leg.TradeLegId);
            var tick = new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionTickDataV2ReadModel
            {
                ContractId = leg.ContractId, ValueDate = plan.ValueDate, TickId = index + 1,
                OptionPrice = (double)leg.CurrentPrice, BidPrice = (double)source.BidPrice!.Value,
                AskPrice = (double)source.AskPrice!.Value, BidSize = 1, AskSize = 1,
                ImpliedVolatility = source.ImpliedVolatility!.Value, UnderlyingPrice = (double)inputs.UnderlyingPrice!.Value,
                Delta = source.Delta!.Value, Gamma = source.Gamma!.Value, Vega = source.Vega!.Value * 100,
                Theta = source.Theta!.Value * 365, TheoreticalPrice = (double)source.TheoreticalPrice!.Value,
                GreeksAvailable = true, SourceGeneration = generation, ObservationRevision = 1,
                ObservedAtUtc = new DateTimeOffset(At.AddMinutes(-index - 1)), CalculatedAtUtc = new DateTimeOffset(At.AddMinutes(-index)),
                ValidUntilUtc = new DateTimeOffset(At.AddSeconds(-5)),
                PricingInputs = new() { Currency = "USD", Multiplier = 50, ExpirationUtc = new(At.AddDays(24)), DaysPerYear = 365,
                    AnnualContinuousRate = .04, Exercise = "European", Premium = "PaidUpfront", BidTickIncrement = .25m,
                    AskTickIncrement = .25m, ModelVersion = "RetainedTest" }
            };
            return leg with { Strike = source.Strike, Expiry = source.Expiry, PutCall = source.PutCall,
                OptionTick = tick, OptionRisk = tick, CashMultiplier = 50 };
        }).ToArray();
        return (plan with { Position = plan.Position with { Legs = legs } }, inputs);
    }

    static (StrategyTradePlanSnapshot Plan, IronCondorDailyRiskInputs Inputs) Capture(int quantity = 1)
    {
        var (legs, risk) = IronCondorOptionCalculatorTests.Evidence(atUtc: new DateTimeOffset(At));
        legs = legs.Select(leg => leg with { SignedQuantity = leg.SignedQuantity * quantity }).ToArray();
        var tradeId = new TradeEntityId(1, 2, 3, 4);
        var trade = new EstablishedTradeDefinition { Id = tradeId, StrategyKind = TradeStrategyKind.IronCondor,
            EstablishedAtUtc = At, Legs = legs };
        var position = new StrategyPositionSnapshot { Id = new(tradeId, Guid.NewGuid()), StrategyKind = TradeStrategyKind.IronCondor,
            ValueDate = DateOnly.FromDateTime(At), DailyPnlPoints = -8 * quantity, AsOfUtc = At, PositionSequence = 1,
            RouteGeneration = 1, IsOpen = true, Legs = legs.Select(leg => new StrategyPositionLeg {
                TradeLegId = leg.TradeLegId, ContractId = leg.ContractId, SignedQuantity = leg.SignedQuantity,
                CurrentPrice = 10, OpeningPrice = 10, LastPriceAtUtc = At }).ToArray() };
        var plan = new StrategyTradePlanSnapshot { Position = position, ValueDate = position.ValueDate, CalculatedAtUtc = At, PlanRevision = 1 };
        var inputs = new IronCondorTradePlanSnapshotCalculator().CaptureScenarioInputs(trade, position, risk,
            StrategyRiskParameterSet.CreateIronCondorDevelopmentDefault(), At);
        return (plan, inputs);
    }
}
