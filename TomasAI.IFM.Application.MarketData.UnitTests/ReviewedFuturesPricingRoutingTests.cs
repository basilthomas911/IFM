using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class ReviewedFuturesPricingRoutingTests
{
    internal static OptionPricingContext Reviewed(OptionExerciseStyle style, bool call = true,
        OptionPremiumStyle premium = OptionPremiumStyle.PaidUpfront)
    {
        var contract = Contract() with
        {
            SchemaVersion = 3,
            Root = premium == OptionPremiumStyle.FuturesStyle ? "SYNTHETIC" : "ES",
            ExerciseStyle = style,
            PremiumStyle = premium,
            UnderlyingKind = PricingUnderlyingKind.Futures,
            Strike = 5000,
            Right = call ? PricingOptionRight.Call : PricingOptionRight.Put,
            PremiumTickRule = OptionPremiumTickRule.Fixed
        };
        return Context() with { Contract = contract, PricerVersion = Black76PricingModel.EngineFor(contract) };
    }

    [Theory]
    [InlineData(OptionExerciseStyle.European, true, OptionPremiumStyle.PaidUpfront)]
    [InlineData(OptionExerciseStyle.European, false, OptionPremiumStyle.FuturesStyle)]
    [InlineData(OptionExerciseStyle.American, true, OptionPremiumStyle.PaidUpfront)]
    [InlineData(OptionExerciseStyle.American, false, OptionPremiumStyle.PaidUpfront)]
    [InlineData(OptionExerciseStyle.American, true, OptionPremiumStyle.FuturesStyle)]
    [InlineData(OptionExerciseStyle.American, false, OptionPremiumStyle.FuturesStyle)]
    public void Reviewed_economics_route_to_qualified_engine(OptionExerciseStyle style, bool call, OptionPremiumStyle premium)
    {
        var context = Reviewed(style, call, premium);
        var result = Black76PricingModel.Calculate(context, Quote("ES-future", 5000),
            Quote("ES-option-call", 100), 5000, call, At);
        Assert.Null(result.Failure);
        Assert.InRange(result.Value!.TheoreticalPrice, 99.99999, 100.00001);
        Assert.True(double.IsFinite(result.Value.Delta));
        var decoded = MessagePackBinarySerializer.Shared.Deserialize<OptionPricingContext>(
            MessagePackBinarySerializer.Shared.Serialize(context)!)!;
        Assert.Equal(context.Contract, decoded.Contract);
        Assert.Equal(context.PricerVersion, decoded.PricerVersion);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void European_es_rho_matches_discounted_premium_sensitivity(bool call)
    {
        var context = Reviewed(OptionExerciseStyle.European, call);
        var result = Black76PricingModel.Calculate(context, Quote("ES-future", 5000),
            Quote("ES-option-call", 100), 5000, call, At);
        Assert.Null(result.Failure);
        var value = result.Value!;
        Assert.True(value.Rho < 0);
        Assert.Equal(-value.TimeToExpiry * value.TheoreticalPrice, value.Rho, 8);
        var request = Black76PricingModel.CreateRequest(context.Contract, 5000, 5000, call,
            value.TimeToExpiry, context.Rate.AnnualContinuousRate);
        var calculator = new TomasAI.IFM.Framework.OptionPricer.Pricing.OptionCalculator();
        const double rateBump = 0.00001;
        var up = calculator.Price(request with { Rate = request.Rate + rateBump }, value.ImpliedVolatility);
        var down = calculator.Price(request with { Rate = request.Rate - rateBump }, value.ImpliedVolatility);
        Assert.True(up.Success);
        Assert.True(down.Success);
        Assert.Equal(value.Rho, (up.Value!.Value.Price - down.Value!.Value.Price) / (2 * rateBump), 7);
    }

    [Fact]
    public void Conflicting_es_settlement_is_rejected_even_after_serialization()
    {
        var context = Reviewed(OptionExerciseStyle.European);
        context = context with { Contract = context.Contract with { PremiumStyle = OptionPremiumStyle.FuturesStyle } };
        var decoded = MessagePackBinarySerializer.Shared.Deserialize<OptionPricingContext>(
            MessagePackBinarySerializer.Shared.Serialize(context)!)!;
        Assert.Equal("ContractConventionConflict", Black76PricingModel.ValidateContext(decoded, At)!.Code);
    }

    [Fact]
    public void Unknown_terms_or_changed_strike_right_engine_never_price()
    {
        var context = Reviewed(OptionExerciseStyle.American);
        OptionPricingPassResult Price(OptionPricingContext c) => Black76PricingModel.Calculate(c,
            Quote("ES-future", 5000), Quote("ES-option-call", 100), 5000, true, At);
        Assert.Equal("ContractMetadataUnavailable", Price(context with
        { Contract = context.Contract with { PremiumStyle = OptionPremiumStyle.Unknown } }).Failure!.Code);
        Assert.Equal("ContractMetadataUnavailable", Price(context with
        { Contract = context.Contract with { Strike = 5000.5m } }).Failure!.Code);
        Assert.Equal("ContractMetadataUnavailable", Price(context with
        { Contract = context.Contract with { Right = PricingOptionRight.Put } }).Failure!.Code);
        Assert.Equal("PricingModelUnsupported", Price(context with { PricerVersion = "Black76.Managed/v1" }).Failure!.Code);
        Assert.Equal("PricingModelUnsupported", Price(context with
        { Contract = context.Contract with { UnderlyingKind = PricingUnderlyingKind.Equity } }).Failure!.Code);
    }
}
