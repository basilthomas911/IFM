using System.Collections.Immutable;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Framework.OptionPricer.Pricing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class IronCondorOptionCalculatorTests
{
    static readonly DateTimeOffset At = new(2026, 9, 8, 16, 0, 0, TimeSpan.Zero);
    static readonly Guid Generation = Guid.Parse("29a8cd30-5aeb-4b87-bfdf-b12cfd8b9dd9");

    [Theory]
    [InlineData(OptionExerciseStyle.European, OptionPremiumStyle.PaidUpfront)]
    [InlineData(OptionExerciseStyle.American, OptionPremiumStyle.PaidUpfront)]
    [InlineData(OptionExerciseStyle.European, OptionPremiumStyle.FuturesStyle)]
    public void Each_leg_uses_exact_qualified_conventions_and_the_combined_price_preserves_sign(OptionExerciseStyle exercise, OptionPremiumStyle premium)
    {
        var (legs, risk) = Evidence(exercise, premium);
        var result = IronCondorOptionCalculator.Calculate(legs, risk.Reverse().ToArray(), At.UtcDateTime);
        result.OptionLegPrices.Should().HaveCount(4);
        result.SignedSpreadPrice.Should().BeLessThan(0);
        result.CombinedSpreadPrice.Should().BeApproximately(-result.SignedSpreadPrice, 1e-10);
        result.OptionLegPrices.Should().OnlyContain(leg => leg.ExerciseStyle == exercise.ToString() && leg.PremiumStyle == premium.ToString());
        var request = Black76PricingModel.CreateRequest(risk[0].Instruments[0].Instrument.Pricing!.Contract,
            5000, legs[0].Strike!.Value, true, 24d/365d, risk[0].Instruments[0].Instrument.Pricing!.Rate.AnnualContinuousRate);
        var expected = new OptionCalculator().TheoreticalPrice(request, 0.2);
        result.OptionLegPrices[0].TheoreticalPrice.Should().BeApproximately(expected.Price!.Value, 1e-10);
        result.OptionLegPrices[0].EngineVersion.Should().Be(expected.EngineVersion);
        // Quantity is applied to currency valuation, never to the per-strategy price.
        var ten = IronCondorOptionCalculator.Calculate(legs.Select(leg => leg with { SignedQuantity = leg.SignedQuantity * 10 }).ToArray(), risk, At.UtcDateTime);
        ten.SignedSpreadPrice.Should().Be(result.SignedSpreadPrice);
        var longTrade = IronCondorOptionCalculator.Calculate(legs.Select(leg => leg with { SignedQuantity = -leg.SignedQuantity }).ToArray(), risk, At.UtcDateTime);
        longTrade.SignedSpreadPrice.Should().Be(-result.SignedSpreadPrice);
        longTrade.CombinedSpreadPrice.Should().Be(result.CombinedSpreadPrice);
    }

    [Fact]
    public void Mixed_leg_expiries_use_each_contract_time_to_expiry()
    {
        var (legs, risk) = Evidence();
        var original = IronCondorOptionCalculator.Calculate(legs, risk, At.UtcDateTime);
        legs[0] = legs[0] with { Expiry = legs[0].Expiry!.Value.AddDays(5) };
        var item = risk[0].Instruments[0];
        var context = item.Instrument.Pricing!;
        var contract = context.Contract with { ExpirationUtc = context.Contract.ExpirationUtc.AddDays(5),
            LastTradingUtc = context.Contract.LastTradingUtc.AddDays(5) };
        risk[0] = risk[0] with { Instruments = [item with {
            Instrument = item.Instrument with { Pricing = context with { Contract = contract } } }] };
        var updated = IronCondorOptionCalculator.Calculate(legs, risk, At.UtcDateTime);
        updated.OptionLegPrices[0].TheoreticalPrice.Should().NotBe(original.OptionLegPrices[0].TheoreticalPrice);
        updated.OptionLegPrices.Skip(1).Select(x => x.TheoreticalPrice).Should()
            .Equal(original.OptionLegPrices.Skip(1).Select(x => x.TheoreticalPrice));
    }

    [Fact]
    public void Stale_missing_or_retired_leg_evidence_never_returns_a_partial_spread()
    {
        var (legs, risk) = Evidence();
        FluentActions.Invoking(() => IronCondorOptionCalculator.Calculate(legs, risk[..3], At.UtcDateTime)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => IronCondorOptionCalculator.Calculate(legs, risk, At.AddSeconds(6).UtcDateTime)).Should().Throw<ArgumentException>();
        risk[0] = risk[0] with { GenerationId = Guid.NewGuid() };
        FluentActions.Invoking(() => IronCondorOptionCalculator.Calculate(legs, risk, At.UtcDateTime)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Individually_qualified_legs_from_different_underlying_futures_cannot_form_a_condor_price()
    {
        var (legs, risk) = Evidence();
        var item = risk[0].Instruments[0];
        var context = item.Instrument.Pricing!;
        var foreign = item.Instrument with
        {
            Pricing = context with { Contract = context.Contract with { UnderlyingContractId = "other-future" } },
            Underlying = item.Instrument.Underlying! with { ContractId = "other-future" }
        };
        risk[0] = risk[0] with { Instruments = [item with { Instrument = foreign }] };
        FluentActions.Invoking(() => IronCondorOptionCalculator.Calculate(legs, risk, At.UtcDateTime))
            .Should().Throw<ArgumentException>().WithMessage("*share one underlying*");
    }

    [Fact]
    public void Calculation_observations_round_trip_with_snapshot_inputs_and_do_not_replace_market_position_prices()
    {
        var (legs, risk) = Evidence();
        var prices = IronCondorOptionCalculator.Calculate(legs, risk, At.UtcDateTime);
        var inputs = new IronCondorTradePlanInputs { CalculatedSpreadPrices = prices };
        var restored = MessagePackSerializer.Deserialize<IronCondorTradePlanInputs>(MessagePackSerializer.Serialize(inputs));
        restored.CalculatedSpreadPrices.Should().BeEquivalentTo(prices);
        var plan = new StrategyTradePlanSnapshot { CalculatedAtUtc = At.UtcDateTime, ValueDate = new(2026, 9, 8),
            Position = new() { MarketValue = -17.25m } };
        var snapshot = IronCondorMonitoringSnapshotCompute.Create(plan, Guid.Empty, inputs);
        snapshot.NetPrice.Should().Be((decimal)prices.CombinedSpreadPrice);
        snapshot.Position.MarketValue.Should().Be(-17.25m);
        var stale = IronCondorMonitoringSnapshotCompute.Create(plan with { CalculatedAtUtc = At.AddMinutes(1).UtcDateTime }, Guid.Empty, inputs);
        stale.NetPrice.Should().BeNull();
    }

    internal static (TradeLegDefinition[] Legs, MarketCompositionSnapshot[] Risk) Evidence(
        OptionExerciseStyle exercise = OptionExerciseStyle.European, OptionPremiumStyle premium = OptionPremiumStyle.PaidUpfront, DateTimeOffset? atUtc = null)
    {
        var at = atUtc ?? At;
        var expiryAt = at.AddDays(24);
        while (expiryAt.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) expiryAt = expiryAt.AddDays(1);
        TradeLegDefinition[] legs =
        [
            new() { ContractId="short-call", TradeLegId=Guid.NewGuid(), Strike=5100, PutCall=1, SignedQuantity=-1, CashMultiplier=50, Expiry=DateOnly.FromDateTime(expiryAt.UtcDateTime) },
            new() { ContractId="long-call", TradeLegId=Guid.NewGuid(), Strike=5200, PutCall=1, SignedQuantity=1, CashMultiplier=50, Expiry=DateOnly.FromDateTime(expiryAt.UtcDateTime) },
            new() { ContractId="short-put", TradeLegId=Guid.NewGuid(), Strike=4900, PutCall=2, SignedQuantity=-1, CashMultiplier=50, Expiry=DateOnly.FromDateTime(expiryAt.UtcDateTime) },
            new() { ContractId="long-put", TradeLegId=Guid.NewGuid(), Strike=4800, PutCall=2, SignedQuantity=1, CashMultiplier=50, Expiry=DateOnly.FromDateTime(expiryAt.UtcDateTime) }
        ];
        var calendar = new OptionPricingCalendar("fixture-calendar/v1", "America/New_York", new(2026,1,1), new(2026,12,31), new(18,0),
            Enumerable.Range(0,365).Select(index => new DateOnly(2026,1,1).AddDays(index)).Where(date => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).ToImmutableArray());
        var conversion = new TreasuryRateConversionPolicy("FinancialModelingPrep", "fixture:CMT", TreasuryRateConvention.UsTreasuryCmtNominalSemiannual, "test/v1", "synthetic-evidence");
        var curve = new TreasuryCurveSnapshot(DateOnly.FromDateTime(at.UtcDateTime), [new(TreasuryTenor.OneMonth,5m)], at, "FinancialModelingPrep");
        var rate = TreasuryRateConversion.Convert(curve, TreasuryTenor.OneMonth, conversion).Value!;
        var scopes = legs.Select((leg,index) =>
        {
            var contract = new OptionPricingConvention
            {
                SchemaVersion=3, ContractId=leg.ContractId, Dataset="GLBX.MDP3", PublisherId=1, InstrumentId=(uint)(index+1), RawSymbol=leg.ContractId,
                Root="ES", Exchange="XCME", Currency="USD", UnderlyingContractId="ES-future", ExerciseStyle=exercise,
                PremiumStyle=premium, UnderlyingKind=PricingUnderlyingKind.Futures, Right=leg.PutCall==1 ? PricingOptionRight.Call : PricingOptionRight.Put,
                Strike=leg.Strike, SettlementStyle=OptionSettlementStyle.DeliveryOfFuture, ExpirationUtc=expiryAt, LastTradingUtc=expiryAt,
                DayCount=PricingDayCount.Actual365Fixed, CalendarVersion=calendar.Version, Multiplier=50, TickSize=0.25m,
                PremiumTickRule=OptionPremiumTickRule.Fixed, TickRuleVersion="fixture-tick/v1", DefinitionDigest=new('a',64), MappingVersion="fixture-mapping/v1", EvidenceId="synthetic-series",
                EffectiveFromUtc=at.AddDays(-10), EffectiveUntilUtc=at.AddDays(40)
            };
            var context = new OptionPricingContext(contract, calendar, rate, at.AddMinutes(1), Generation, Black76PricingModel.EngineFor(contract),5000,1000,"fixture-publication/v1");
            var quote = new OptionPricingQuote(leg.ContractId,10,11,10,10,at,at,1,Generation);
            var underlying = quote with { ContractId="ES-future", Bid=4999.75m, Ask=5000.25m };
            return new MarketCompositionSnapshot(1, Guid.NewGuid(),leg.ContractId,"qualified","Daily",Generation,at,at.AddSeconds(5),
                [new(new(leg.ContractId,quote,context,leg.Strike,leg.PutCall==1,underlying),new(0.2,0.1,0.01,0,0,0,5,24d/365,"fixture-pricing"))],"digest");
        }).ToArray();
        return (legs,scopes);
    }
}
