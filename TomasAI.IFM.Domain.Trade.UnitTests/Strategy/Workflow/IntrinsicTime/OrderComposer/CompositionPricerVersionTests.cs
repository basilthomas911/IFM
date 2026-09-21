using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Framework.OptionPricer.Pricing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;

public sealed class CompositionPricerVersionTests
{
    [Fact]
    public void Version_preserves_concrete_engine_identity_for_immutable_catalog_content()
    {
        var request = new OptionPricingRequest(
            UnderlyingKind.Futures, ExerciseKind.European, PremiumKind.PaidUpfront,
            OptionSide.Call, 1, 1, 1, 0);

        var expected = OptionCalculator.EngineVersionFor(request) + "/Decimal12-ToEven-v1";

        Assert.Equal(expected, new Black76ComposerPricer().Version);
        Assert.DoesNotContain(OptionCalculator.Version, new Black76ComposerPricer().Version);
    }
}
