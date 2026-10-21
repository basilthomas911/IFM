using MessagePack;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.TestSupport;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class IronCondorForwardPriceTests
{
    [Fact]
    public void Positive_raw_sum_is_not_negated_for_short_positions_and_wire_evidence_round_trips()
    {
        var request = IronCondorForwardPriceTestScenario.Request();
        var price = IronCondorForwardPriceCalculation.Calculate(request);
        Assert.Equal(15.6m, price.PutForwardPricePoints); Assert.Equal(14.4m, price.CallForwardPricePoints);
        Assert.Equal("IronCondor/DeltaTimeStrength020RawSum/v2", price.ModelVersion);
        Assert.Equal(30m, price.ForwardPrice);
        var restored = MessagePackSerializer.Deserialize<IronCondorForwardPrice>(MessagePackSerializer.Serialize(price));
        Assert.Equal(request.Fingerprint(), restored.InputFingerprint);
        Assert.Equal(request.IronCondorForwardPriceInputs, restored.IronCondorForwardPriceInputs with { OptionLegs = request.IronCondorForwardPriceInputs.OptionLegs });
        Assert.Equal(request.IronCondorForwardPriceInputs.OptionLegs, restored.IronCondorForwardPriceInputs.OptionLegs);
    }

    [Fact]
    public void Fixed_strength_preserves_maximum_time_amplification_at_entry_and_both_directions()
    {
        Assert.Equal(11d, IronCondorForwardPriceCalculation.AdjustSpread(10, 10, -.5, 40, 40, true), 10);
        Assert.Equal(9d, IronCondorForwardPriceCalculation.AdjustSpread(10, 10, .5, 40, 40, false), 10);
        Assert.Equal(10.5d, IronCondorForwardPriceCalculation.AdjustSpread(10, 10, .5, 10, 40, true), 10);
        Assert.Equal(9.5d, IronCondorForwardPriceCalculation.AdjustSpread(10, 10, -.5, 10, 40, false), 10);
    }

    [Theory]
    [InlineData("IronCondor/EntryCreditForwardPnl/v2", 10.82165536060817, -10.82165536060817)]
    [InlineData("LegacyScenario/v1", -10.82165536060817, -10.82165536060817)]
    public void Chart_projection_uses_signed_position_points_without_changing_raw_closing_cost(string version, double raw, double chart)
    {
        var snapshot = new TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanSnapshot {
            CalculationVersion = version, CombinedSpreadPrice = -10.75m,
            ForwardPrice = (decimal)raw, CombinedForwardPrice = (decimal)raw };
        Assert.Equal((decimal)chart, snapshot.ForwardSpreadForChart);
        Assert.Equal((decimal)raw, snapshot.ForwardPrice);
        Assert.Equal((decimal)raw, snapshot.CombinedForwardPrice);
        var restored = MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanSnapshot>(MessagePackSerializer.Serialize(snapshot));
        Assert.Equal(snapshot.ForwardSpreadForChart, restored.ForwardSpreadForChart);
        var json = System.Text.Json.JsonSerializer.SerializeToElement(snapshot, new System.Text.Json.JsonSerializerOptions {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        Assert.Equal((decimal)chart, json.GetProperty("forwardSpreadForChart").GetDecimal());
    }

    [Theory]
    [InlineData(12.5, 3, -375, .75, false)]
    [InlineData(12.5, 4, -500, 1, true)]
    [InlineData(13, 4, -600, 1.2, true)]
    [InlineData(10, 4, 0, 0, false)]
    [InlineData(7.5, 4, 0, 0, false)]
    [InlineData(0, 4, 0, 0, false)]
    public void Entry_credit_minus_closing_cost_exits_at_the_exact_currency_threshold(double value, int quantity, double expectedLoss, double ratio, bool exit)
    {
        var completed = new IronCondorForwardPriceCalculatedEvent { CommandId = Guid.NewGuid(),
            IronCondorForwardPrice = new() { ForwardPrice = (decimal)value, ContractMultiplier = 50, StrategyQuantity = quantity, Currency = "CAD" } };
        var risk = IronCondorForwardPriceCalculation.CalculateRisk(completed, 10, 500);
        Assert.Equal((decimal)expectedLoss, risk.ForwardLoss); Assert.Equal(ratio, risk.ForwardLossRatio,10);
        Assert.Equal(exit,risk.ExitRecommended);
        Assert.Equal(10m,risk.EntryCreditPoints);
        Assert.Equal((10m-(decimal)value)*50*quantity,risk.ForwardPnl);
        Assert.Equal(IronCondorForwardRisk.EntryCreditCalculationVersion,risk.CalculationVersion);
    }

    [Fact]
    public async Task Higher_projected_closing_cost_reaches_plan_and_exits_despite_positive_daily_pnl()
    {
        var request = IronCondorForwardPriceTestScenario.Request();
        request = request with { Position = request.Position with { Legs = request.Position.Legs.Select(leg => leg with {
            OpeningPrice = leg.SignedQuantity < 0 ? 10m : 5m }).ToArray() } };
        var price = IronCondorForwardPriceCalculation.Calculate(request);
        Assert.Equal(30m,price.ForwardPrice);
        var completed = new FunctionEventContext<CalculateIronCondorForwardPriceCommand>(typeof(IronCondorForwardPriceCalculatedEvent),request,price).Complete(TimeProvider.System).Completed!;
        Assert.Equal(request.CommandId,completed.CommandId);
        var planRequest = IronCondorForwardPriceTestScenario.PlanRequest(request,completed);
        var context = Substitute.For<IIronCondorTradePlanFunctionContext>(); context.Algorithm.Returns(new IronCondorTradePlanAlgorithm());
        var result = await planRequest.ExecuteAsync(new IronCondorTradePlanFunctionState().Prepare(planRequest),context,input => input.Complete(TimeProvider.System),default);
        var plan = result.Completed!.Plan;
        Assert.Equal(30m,plan.ForwardTradePrice);
        Assert.Equal(10m,plan.IronCondorTradePlanSnapshot!.IronCondorForwardRisk!.EntryCreditPoints);
        Assert.Equal(-1000m,plan.IronCondorTradePlanSnapshot.IronCondorForwardRisk.ForwardLoss);
        Assert.Equal(-1000m,plan.ForwardPnl);
        Assert.Equal(1000m,plan.ForwardLoss);
        Assert.True(plan.IronCondorTradePlanSnapshot.DailyPnl > 0);
        Assert.True(plan.RequiresExit);
        Assert.Null(plan.IronCondorTradePlanSnapshot.ForwardPriceCalculationError);
    }

    [Fact]
    public void Zero_theoretical_spreads_and_zero_marked_spreads_generate_a_zero_forward_price()
    {
        var request = IronCondorForwardPriceTestScenario.Request();
        var legs = request.IronCondorForwardPriceInputs.OptionLegs.Select(leg => leg with { TheoreticalPrice = 10, MarkPrice = 10 }).ToArray();
        request = request with { IronCondorForwardPriceInputs = request.IronCondorForwardPriceInputs with { OptionLegs = legs } };
        Assert.Equal(0m,IronCondorForwardPriceCalculation.Calculate(request).ForwardPrice);
    }

    [Fact]
    public void Scaling_uses_the_observed_multiplier_instead_of_assuming_fifty()
    {
        var completed = new IronCondorForwardPriceCalculatedEvent { CommandId = Guid.NewGuid(),
            IronCondorForwardPrice = new() { ForwardPrice = 12.5m, ContractMultiplier = 20, StrategyQuantity = 3, Currency = "CAD" } };
        var risk = IronCondorForwardPriceCalculation.CalculateRisk(completed,10,500);
        Assert.Equal(-150m,risk.ForwardLoss); Assert.Equal(.3,risk.ForwardLossRatio,10); Assert.False(risk.ExitRecommended);
    }

    [Fact]
    public void Historical_v1_risk_payload_has_no_invented_entry_basis_or_projected_pnl()
    {
        var bytes = MessagePackSerializer.Serialize(new IronCondorForwardRisk {
            CalculationVersion = "IronCondor/SignedTotalForwardValue/v1", ForwardLoss = -1500m });
        Assert.Equal(0x9a,bytes[0]);
        bytes[0] = 0x98; // Original eight-key payload, before the appended entry/PnL evidence.
        var restored = MessagePackSerializer.Deserialize<IronCondorForwardRisk>(bytes[..^2]);
        Assert.Equal("IronCondor/SignedTotalForwardValue/v1",restored.CalculationVersion);
        Assert.Equal(-1500m,restored.ForwardLoss);
        Assert.Null(restored.EntryCreditPoints); Assert.Null(restored.ForwardPnl);
    }

    [Fact]
    public void Missing_entry_basis_never_becomes_a_successful_profit_projection()
    {
        var request = IronCondorForwardPriceTestScenario.Request();
        request = request with { Position = request.Position with { Legs = request.Position.Legs.Select(leg => leg with { OpeningPrice=0m }).ToArray() } };
        var price = IronCondorForwardPriceCalculation.Calculate(request);
        var completed = new FunctionEventContext<CalculateIronCondorForwardPriceCommand>(typeof(IronCondorForwardPriceCalculatedEvent),request,price).Complete(TimeProvider.System).Completed!;
        var command = IronCondorForwardPriceTestScenario.PlanRequest(request,completed);
        var snapshot = new IronCondorTradePlanSnapshotCalculator().CreateFromForwardPrice(
            new() { Position=request.Position,ValueDate=request.EntityId.ValueDate,CalculatedAtUtc=request.RequestedAtUtc },command);
        Assert.Equal(0m,snapshot.ForwardPrice);
        Assert.Contains("ENTRY_CREDIT.UNAVAILABLE",snapshot.ForwardPriceCalculationError);
        Assert.Null(snapshot.IronCondorForwardRisk);
    }

    [Theory]
    [InlineData(0, -10)]
    [InlineData(-1, -10)]
    [InlineData(10, 10)]
    public void Invalid_calendar_intervals_report_exact_dates_and_day_counts(int maturityOffset, int tradeOffset)
    {
        var request = IronCondorForwardPriceTestScenario.Request();
        var date = request.EntityId.ValueDate;
        request = request with { IronCondorForwardPriceInputs = request.IronCondorForwardPriceInputs with {
            TradeDate = date.AddDays(tradeOffset), MaturityDate = date.AddDays(maturityOffset) } };
        var error = Assert.Throws<ArgumentException>(() => IronCondorForwardPriceCalculation.Calculate(request));
        Assert.Contains("IronCondorForwardPrice.DATES.INVALID", error.Message);
        Assert.Contains($"TradeDate={date.AddDays(tradeOffset):yyyy-MM-dd}", error.Message);
        Assert.Contains($"MaturityDate={date.AddDays(maturityOffset):yyyy-MM-dd}", error.Message);
        Assert.Contains($"ValueDate={date:yyyy-MM-dd}", error.Message);
        Assert.Contains($"RemainingDays={maturityOffset}", error.Message);
        Assert.Contains($"OpeningDays={maturityOffset-tradeOffset}", error.Message);
    }

    [Fact]
    public void Missing_or_mismatched_leg_inputs_are_rejected_without_inventing_prices()
    {
        var request = IronCondorForwardPriceTestScenario.Request();
        Assert.Throws<ArgumentException>(() => IronCondorForwardPriceCalculation.Calculate(request with {
            IronCondorForwardPriceInputs = request.IronCondorForwardPriceInputs with { OptionLegs = request.IronCondorForwardPriceInputs.OptionLegs[..3] } }));
        var legs = request.IronCondorForwardPriceInputs.OptionLegs.ToArray(); legs[0] = legs[0] with { Delta = double.NaN };
        Assert.Throws<ArgumentException>(() => IronCondorForwardPriceCalculation.Calculate(request with {
            IronCondorForwardPriceInputs = request.IronCondorForwardPriceInputs with { OptionLegs = legs } }));
    }
}
