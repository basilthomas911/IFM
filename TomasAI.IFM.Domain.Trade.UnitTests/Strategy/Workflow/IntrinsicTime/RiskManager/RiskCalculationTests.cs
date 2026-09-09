using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using System.Collections.Immutable;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;

[Trait("Category", "PortfolioFinancial")]
public sealed class RiskCalculationTests
{
    public static TheoryData<string> ComposerVariants => new(CompositionFixture.Variants);

    [Theory, MemberData(nameof(ComposerVariants))]
    public async Task Reads_and_reprices_the_actual_composer_candidate_for_each_variant(string variant)
    {
        var command = await CompositionFixture.Command(variant);
        var composition = new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer(new Black76ComposerPricer()).Calculate(command);
        composition.Outcome.Should().Be(CompositionOutcome.Composed);
        var candidate = composition.Candidate!;
        var legs = RiskUnitModel.ReadLegs(candidate, command.MarketSnapshot, command.EvaluatedAtUtc);
        var risk = RiskUnitModel.Calculate(legs, candidate.Pricing.WorstDebit, candidate.Pricing.CostReserve, 0,
            candidate.RiskEvidence.PlannedLoss, candidate.RiskEvidence.StressLoss);
        risk.GrossContracts.Should().Be(candidate.Legs.Length);
        risk.LossCharge.Should().BeGreaterThan(0);
        if (candidate.RiskEvidence.MaximumLoss is { } maximum)
            risk.MaximumLoss.Should().Be(decimal.Ceiling(maximum * 100) / 100);
    }

    [Fact]
    public async Task Rehashed_tampered_valuation_is_rejected_against_frozen_quotes()
    {
        var command = await CompositionFixture.Command("BullCallDebit");
        var candidate = new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer(new Black76ComposerPricer()).Calculate(command).Candidate!;
        candidate = candidate with { Legs=candidate.Legs.SetItem(0,candidate.Legs[0] with { Valuation=candidate.Legs[0].Valuation! with { Delta=999 } }) };
        candidate = candidate with { CandidateHash=CompositionHash.Candidate(candidate) };
        var action = () => RiskUnitModel.ReadLegs(candidate, command.MarketSnapshot, command.EvaluatedAtUtc);
        action.Should().Throw<RiskCalculationException>().Which.ReasonCode.Should().Be("RM.INPUT.VALUATION_MISMATCH");
    }

    public static TheoryData<bool, bool, decimal, decimal> Verticals => new()
    {
        { true, true, 4m, 210m }, { true, false, -4m, 310m },
        { false, true, 4m, 210m }, { false, false, -4m, 310m }
    };

    [Theory, MemberData(nameof(Verticals))]
    public void Four_verticals_recompute_payoff_and_charge_cost_once(bool call, bool debit, decimal premium, decimal expected)
    {
        int lowSign = call == debit ? 1 : -1;
        var result = RiskUnitModel.Calculate([Option("L", call, 4990, lowSign), Option("H", call, 5000, -lowSign)],
            premium, 10, 7, null, null);
        result.MaximumLoss.Should().Be(expected);
        result.LossCharge.Should().BeGreaterThanOrEqualTo(expected + 7);
        result.Scenarios.Should().Be(36);
        result.GrossContracts.Should().Be(2);
        result.GrossNotional.Should().Be(500000);
        result.SettlementCash.Should().Be(debit ? 200 : 0);
    }

    [Theory]
    [InlineData(false, "Balanced", 10, 10, -4, 310)]
    [InlineData(false, "Bullish", 10, 20, -4, 810)]
    [InlineData(false, "Bearish", 20, 10, -4, 810)]
    [InlineData(true, "Balanced", 10, 10, 4, 210)]
    [InlineData(true, "Bullish", 10, 20, 4, 210)]
    [InlineData(true, "Bearish", 20, 10, 4, 210)]
    public void All_condor_variants_include_unequal_wing_payoffs(bool isLong, string bias, int putWidth, int callWidth,
        int premium, int expected)
    {
        int sign = isLong ? -1 : 1;
        var legs = ImmutableArray.Create(Option("P0", false, 4990-putWidth, sign), Option("P1", false, 4990, -sign),
            Option("C0", true, 5010, -sign), Option("C1", true, 5010+callWidth, sign));
        RiskUnitModel.Calculate(legs, premium, 10, 0, null, null).MaximumLoss.Should().Be(expected, bias);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void Long_and_short_future_keep_unbounded_loss_and_use_stress_without_pretending_stop_is_guaranteed(int sign)
    {
        var leg = new RiskLegInput("ES", "ES", true, false, 0, sign, 50, 5000, 0, 0, 0, 1, 0, 0, 0);
        var result = RiskUnitModel.Calculate([leg], sign * 5000, 10, 20, 1010, 5010);
        result.MaximumLoss.Should().BeNull();
        result.ScenarioLoss.Should().Be(50010);
        result.LossCharge.Should().Be(50030);
        result.SettlementCash.Should().Be(0);
        result.Delta.Should().Be(sign * 50);
    }

    [Fact]
    public void Greek_units_are_multiplier_adjusted_vega_per_point_and_theta_per_day()
    {
        var a = Option("L", true, 4990, 1) with { Delta=.6m, Gamma=.004m, Vega=100, Theta=-365 };
        var b = Option("H", true, 5000, -1) with { Delta=.4m, Gamma=.003m, Vega=80, Theta=-292 };
        var result = RiskUnitModel.Calculate([a,b], 4, 10, 0, null, null);
        result.Delta.Should().Be(10); result.Gamma.Should().Be(.05m);
        result.VegaPerPoint.Should().Be(10); result.ThetaPerDay.Should().Be(-10);
    }

    [Fact]
    public void Unbounded_option_ratio_and_mixed_underlying_are_invalid_inputs()
    {
        var unbounded = () => RiskUnitModel.Calculate([Option("L", true, 4990, 2), Option("H", true, 5000, -1)], 4, 10, 0, null, null);
        unbounded.Should().Throw<RiskCalculationException>().Which.ReasonCode.Should().Be("RM.CALCULATION.UNBOUNDED_OPTIONS");
        var mixed = () => RiskUnitModel.Calculate([Option("L", true, 4990, 1), Option("H", true, 5000, -1) with { UnderlyingId="NQ" }], 4, 10, 0, null, null);
        mixed.Should().Throw<RiskCalculationException>();
    }

    [Fact]
    public void Scenario_cancellation_is_observed()
    {
        var action = () => RiskUnitModel.Calculate([Option("L", true, 4990, 1), Option("H", true, 5000, -1)],
            4, 10, 0, null, null, new CancellationToken(true));
        action.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Quantity_specific_margin_selects_largest_feasible_quantity_without_monotonic_assumption()
    {
        var (authority, funding) = SizingFixture();
        funding = [funding[0] with { MarginFunding=100 }, funding[1] with { MarginFunding=900 }, funding[2] with { MarginFunding=200 }];
        var result = RiskSizingModel.Calculate(Unit, new(TimeFrameType.Daily, 3), authority with { AvailableCash=500 }, 3, funding, 1);
        result.StrategyUnits.Should().Be(3);
        result.Requirements!.PositionSlots.Should().Be(1);
        result.Requirements.MarginFunding.Should().Be(200);
        result.Requirements.GrossContracts.Should().Be(6);
        result.Requirements.ContentHash.Should().Be(FinancialCanonicalHash.Requirements(result.Requirements));
    }

    [Fact]
    public void Missing_quantity_quote_is_failure_not_silent_smaller_approval()
    {
        var (authority, funding) = SizingFixture();
        var action = () => RiskSizingModel.Calculate(Unit, new(TimeFrameType.Daily, 3), authority, 3, [funding[0]], 1);
        action.Should().Throw<RiskCalculationException>().Which.ReasonCode.Should().Be("RM.MARGIN.QUANTITY_MISSING");
    }

    [Fact]
    public void Most_restrictive_loss_cash_and_deployment_caps_apply()
    {
        var (authority, funding) = SizingFixture();
        var limited = authority with
        {
            Limits = authority.Limits.Select(x => x.ScopeKind == CapacityScopeKind.Deployment && x.Measure == CapacityMeasure.GrossContracts
                ? x with { Maximum=4 } : x).ToImmutableArray()
        };
        RiskSizingModel.Calculate(Unit, new(TimeFrameType.Daily, 3), limited, 3, funding, 1).StrategyUnits.Should().Be(2);
        RiskSizingModel.Calculate(Unit, new(TimeFrameType.Daily, 3), authority with { PerTradeLossBudget=100 }, 3, funding, 1).StrategyUnits.Should().Be(1);
        var rejected = RiskSizingModel.Calculate(Unit, new(TimeFrameType.Daily, 3), authority with { AvailableCash=0 }, 3, funding, 1);
        rejected.StrategyUnits.Should().Be(0); rejected.Requirements.Should().BeNull();
    }

    [Fact]
    public void Opposite_pending_greeks_do_not_cancel_each_other_for_admission()
    {
        var (authority, funding) = SizingFixture();
        authority = authority with
        {
            Limits = authority.Limits.Select(x => x.Measure == CapacityMeasure.Delta ? x with { Maximum=30 } : x).ToImmutableArray(),
            Usage = [new(CapacityScopeKind.Underlying, "ES", CapacityMeasure.Delta, CapacityUnit.NormalizedDelta, 10, -10, 0)]
        };
        RiskSizingModel.Calculate(Unit, new(TimeFrameType.Daily, 3), authority, 3, funding, 1).StrategyUnits.Should().Be(1);
    }

    [Theory]
    [InlineData(TimeFrameType.Daily)]
    [InlineData(TimeFrameType.Weekly)]
    [InlineData(TimeFrameType.Monthly)]
    public void Each_triggering_timeframe_has_a_separate_default_policy(TimeFrameType horizon)
    {
        var policy = RiskSizingPolicy.Default(horizon);
        policy.Horizon.Should().Be(horizon); policy.MaximumUnits.Should().Be(10); policy.PerTradeRiskFraction.Should().Be(.01m);
    }

    static RiskLegInput Option(string id, bool call, decimal strike, int sign) =>
        new(id, "ES", false, call, strike, sign, 50, 5000, 0, .2m, .1m, call ? .5m : -.5m, .003m, 100, -365);
    static readonly RiskUnitResult Unit = new(100, 100, 100, 50, 500000, 2, 10, .05m, 10, -10, 36, 2);

    [Fact]
    public void Quantity_fee_quote_adds_only_the_shortfall_above_already_included_composer_fees()
    {
        var (authority,funding)=SizingFixture();
        var unchanged=RiskSizingModel.Requirements(Unit,authority,funding[1]);
        unchanged.LossCharge.Should().Be(200); unchanged.FeeReserve.Should().Be(4);
        var higher=RiskSizingModel.Requirements(Unit,authority,funding[1] with { EntryFees=10 });
        higher.LossCharge.Should().Be(206); higher.FeeReserve.Should().Be(10);
    }

    static (RiskSizingAuthority, ImmutableArray<RiskQuantityFunding>) SizingFixture()
    {
        var now = new DateTime(2026,9,8,14,0,0,DateTimeKind.Utc);
        var evidence = new FinancialEvidenceReference { EvidenceId=Guid.NewGuid(), Version=1, ContentHash=new('A',64),
            Source="EmulatorMarginFixture/v1", Environment="Test", ObservedAtUtc=now, ValidUntilUtc=now.AddSeconds(1) };
        var authority = new RiskSizingAuthority(1,2,new CatalogKey(default,Guid.NewGuid(),1),"ES",10000,100000,10000,[],[],now,now.AddSeconds(1),"Test");
        var funding = Enumerable.Range(1,3).Select(q => new RiskQuantityFunding(q,100*q,100*q,2*q,0,evidence)).ToImmutableArray();
        var template = RiskSizingModel.Requirements(Unit,authority,funding[0]);
        authority = authority with { Limits = template.Exposures.Select(x => new CapacityLimit(x.ScopeKind,x.ScopeKey,x.Measure,x.Unit,100000000)).ToImmutableArray() };
        return (authority,funding);
    }
}
