using TomasAI.IFM.Framework.OptionPricer.Pricing;

namespace TomasAI.IFM.Framework.OptionPricer.UnitTests;

public class AmericanPricingQualificationTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Theory]
    [InlineData(UnderlyingKind.Equity, OptionSide.Call, .04, .08, PremiumKind.PaidUpfront)]
    [InlineData(UnderlyingKind.Equity, OptionSide.Put, -.02, .01, PremiumKind.PaidUpfront)]
    [InlineData(UnderlyingKind.Equity, OptionSide.Call, -.02, 0, PremiumKind.PaidUpfront)]
    [InlineData(UnderlyingKind.Futures, OptionSide.Call, .05, 0, PremiumKind.PaidUpfront)]
    [InlineData(UnderlyingKind.Futures, OptionSide.Put, -.02, 0, PremiumKind.PaidUpfront)]
    [InlineData(UnderlyingKind.Futures, OptionSide.Put, .05, 0, PremiumKind.FuturesStyle)]
    public void CrrAgreesWithIndependentMomentMatchedTrinomial(
        UnderlyingKind asset, OptionSide side, double rate, double yield, PremiumKind premium)
    {
        var r = new OptionPricingRequest(asset, ExerciseKind.American, premium, side,
            100, 100, .75, rate, yield == 0 ? DividendKind.None : DividendKind.ContinuousYield, yield);
        var coarse = new OptionCalculator(new() { Steps = 801 }).Price(r, .25);
        var fine = new OptionCalculator(new() { Steps = 1601 }).Price(r, .25);
        Assert.True(coarse.Success && fine.Success);
        var g = fine.Value!.Value;
        var referenceError = Math.Abs(g.Price - Trinomial(r, .25));
        output.WriteLine($"Trinomial price error: {referenceError:G17}; CRR resolution error: {Math.Abs(g.Price - coarse.Value!.Value.Price):G17}");
        Assert.InRange(referenceError, 0, .015);
        Assert.InRange(Math.Abs(g.Price - coarse.Value!.Value.Price), 0, .008);
        Assert.InRange(Math.Abs(g.Delta - coarse.Value.Value.Delta), 0, .003);
        Assert.InRange(Math.Abs(g.Gamma - coarse.Value.Value.Gamma), 0, .003);
        Assert.InRange(Math.Abs(g.Vega - coarse.Value.Value.Vega), 0, .15);
        Assert.InRange(Math.Abs(g.Theta - coarse.Value.Value.Theta), 0, .1);
        Assert.InRange(Math.Abs(g.Rho - coarse.Value.Value.Rho), 0, .2);
        var euro = new OptionCalculator().TheoreticalPrice(r with { Exercise = ExerciseKind.European }, .25);
        Assert.True(g.Price >= euro.Price - .005);
    }

    // Independent log-price, three-branch moment matching (variance=sigma² dt);
    // no production lattice code or pricing API is used by this reference.
    private static double Trinomial(OptionPricingRequest r, double sigma)
    {
        const int n = 800;
        double dt = r.TimeToExpiry / n, variance = sigma * sigma * dt;
        double dx = Math.Sqrt(3 * variance);
        double carry = r.Underlying == UnderlyingKind.Futures ? 0 : r.Rate - r.DividendYield;
        double mean = (carry - sigma * sigma / 2) * dt;
        double up = (variance + mean * mean + mean * dx) / (2 * dx * dx);
        double down = (variance + mean * mean - mean * dx) / (2 * dx * dx);
        double middle = 1 - up - down;
        Assert.True(up > 0 && down > 0 && middle > 0);
        double discount = Math.Exp(-(r.Premium == PremiumKind.FuturesStyle ? 0 : r.Rate) * dt);
        int sign = r.Side == OptionSide.Call ? 1 : -1;
        double Payoff(int j) => Math.Max(sign * (r.UnderlyingPrice * Math.Exp(j * dx) - r.Strike), 0);
        var a = new double[2 * n + 3];
        var b = new double[a.Length];
        int offset = n + 1;
        for (int j = -n; j <= n; j++) a[offset + j] = Payoff(j);
        for (int i = n - 1; i >= 0; i--)
        {
            for (int j = -i; j <= i; j++)
                b[offset + j] = Math.Max(Payoff(j), discount * (down * a[offset + j - 1] +
                    middle * a[offset + j] + up * a[offset + j + 1]));
            (a, b) = (b, a);
        }
        return a[offset];
    }

    [Fact]
    public void ExercisePlateauGreeksDoNotInventTimeValue()
    {
        var r = new OptionPricingRequest(UnderlyingKind.Equity, ExerciseKind.American,
            PremiumKind.PaidUpfront, OptionSide.Put, 20, 100, .1, .2);
        var g = new OptionCalculator().Price(r, .1).Value!.Value;
        Assert.Equal(80, g.Price);
        Assert.InRange(g.Delta, -1.000001, -.999999);
        Assert.InRange(Math.Abs(g.Gamma), 0, 1e-6);
        Assert.InRange(Math.Abs(g.Vega) + Math.Abs(g.Rho) + Math.Abs(g.Theta), 0, 1e-6);
    }

    [Fact]
    public void NearExpiryAndDeepMoneynessRemainBoundedAndInvalidPolicyIsRejected()
    {
        var c = new OptionCalculator();
        foreach (var spot in new[] { 10d, 100d, 1000d })
        foreach (var style in new[] { ExerciseKind.European, ExerciseKind.American })
        foreach (var side in new[] { OptionSide.Call, OptionSide.Put })
        {
            var r = new OptionPricingRequest(UnderlyingKind.Futures, style, PremiumKind.PaidUpfront,
                side, spot, 100, 1e-6, -.01);
            var p = c.Price(r, .2);
            Assert.True(p.Success, p.Failure.ToString());
            Assert.InRange(p.Value!.Value.Price, 0, Math.Max(spot, 100) * 1.00001);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new OptionCalculator(new() { Steps = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OptionCalculator(new() { SpatialSteps = 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OptionCalculator(new() { SorTolerance = double.NaN }));
        Assert.Throws<ArgumentException>(() => c.PriceBatch(new OptionPricingRequest[2049], new double[2049], new PricingResult[2049]));
    }
}
