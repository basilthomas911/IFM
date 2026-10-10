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
        var position = plan.Position with { PositionSequence = 2, DailyPnl = -12,
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
        var profit = calculator.Create(plan with { Position = plan.Position with { DailyPnl = 1 } }, Guid.NewGuid(), null, null, inputs);
        profit.ForwardLossRatio.Should().BeGreaterThan(0);
        profit.ExitRecommended.Should().BeFalse();
    }

    [Fact]
    public void Missing_market_quotes_do_not_hide_known_daily_pnl_or_invent_forward_loss()
    {
        var (plan, _) = Capture();
        var inputs = IronCondorTradePlanSnapshotCalculator.CaptureUnavailableInputs(plan.Position,
            new() { ContractCashMultiplier = 50, OpeningCommission = 20, TradeDate = plan.ValueDate });
        var snapshot = new IronCondorTradePlanSnapshotCalculator().Create(plan, Guid.NewGuid(), plan.ValueDate, null, inputs);
        snapshot.DailyPnl.Should().Be(-420);
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

    static (StrategyTradePlanSnapshot Plan, IronCondorDailyRiskInputs Inputs) Capture(int quantity = 1)
    {
        var (legs, risk) = IronCondorOptionCalculatorTests.Evidence(atUtc: new DateTimeOffset(At));
        legs = legs.Select(leg => leg with { SignedQuantity = leg.SignedQuantity * quantity }).ToArray();
        var tradeId = new TradeEntityId(1, 2, 3, 4);
        var trade = new EstablishedTradeDefinition { Id = tradeId, StrategyKind = TradeStrategyKind.IronCondor,
            EstablishedAtUtc = At, Legs = legs };
        var position = new StrategyPositionSnapshot { Id = new(tradeId, Guid.NewGuid()), StrategyKind = TradeStrategyKind.IronCondor,
            ValueDate = DateOnly.FromDateTime(At), DailyPnl = -8 * quantity, AsOfUtc = At, PositionSequence = 1,
            RouteGeneration = 1, IsOpen = true, Legs = legs.Select(leg => new StrategyPositionLeg {
                TradeLegId = leg.TradeLegId, ContractId = leg.ContractId, SignedQuantity = leg.SignedQuantity,
                CurrentPrice = 10, OpeningPrice = 10, LastPriceAtUtc = At }).ToArray() };
        var plan = new StrategyTradePlanSnapshot { Position = position, ValueDate = position.ValueDate, CalculatedAtUtc = At, PlanRevision = 1 };
        var inputs = new IronCondorTradePlanSnapshotCalculator().CaptureScenarioInputs(trade, position, risk,
            StrategyRiskParameterSet.CreateIronCondorDevelopmentDefault(), At);
        return (plan, inputs);
    }
}
