using System.Diagnostics;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;

namespace TomasAI.IFM.Framework.OptionPricer.Pricing;

public class OptionSpreadPricer
{

    public static (ICollection<OptionSpreadResult> PutSpreadResult, ICollection<OptionSpreadResult> CallSpreadResult, double Duration) PriceIronCondor(
        CreditSpreadPricerArgs putCreditSpreadArgs,
        CreditSpreadPricerArgs callCreditSpreadArgs)
    {
        var sw = Stopwatch.StartNew();

        var putResult = PriceCreditSpread(putCreditSpreadArgs, -1);
        var callResult = PriceCreditSpread(callCreditSpreadArgs, 1);

        sw.Stop();

        return (
            PutSpreadResult: new List<OptionSpreadResult> { putResult },
            CallSpreadResult: new List<OptionSpreadResult> { callResult },
            Duration: sw.Elapsed.TotalMilliseconds);
    }

    static OptionSpreadResult PriceCreditSpread(CreditSpreadPricerArgs args, int optionType)
    {
        double forwardPrice = Convert.ToDouble(args.AssetPrice);
        double timeToMaturity = args.DaysToMaturity / 365.0;

        var exercise = args.OptionStyle == OptionStyle.American ? ExerciseKind.American : ExerciseKind.European;
        var side = optionType > 0 ? OptionSide.Call : OptionSide.Put;
        var calculator = new OptionCalculator();
        var shortRequest = new OptionPricingRequest(UnderlyingKind.Futures, exercise, PremiumKind.PaidUpfront,
            side, forwardPrice, args.ShortStrike, timeToMaturity, args.RiskFreeRate);
        var longRequest = shortRequest with { Strike = args.LongStrike };
        var shortResult = calculator.TheoreticalPrice(shortRequest, args.ShortImpliedVolatility);
        var longResult = calculator.TheoreticalPrice(longRequest, args.LongImpliedVolatility);
        if (!shortResult.Success || !longResult.Success)
            throw new InvalidOperationException("Unable to price option spread with the selected contract conventions.");
        double shortPrice = shortResult.Price!.Value;
        double longPrice = longResult.Price!.Value;

        var result = new OptionSpreadResult(
            0,
            args.DaysToMaturity,
            forwardPrice,
            args.RiskFreeRate,
            args.RateOfReturn,
            args.ShortStrike,
            args.ShortImpliedVolatility,
            args.LongStrike,
            args.LongImpliedVolatility);

        result.ShortValues.Add([shortPrice]);
        result.ShortComplete = true;
        result.LongValues.Add([longPrice]);
        result.LongComplete = true;

        return result;
    }

}
