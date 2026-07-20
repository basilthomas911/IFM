using System.Diagnostics;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.Framework.OptionPricer.Black76;

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

        double shortPrice = OptionModel.Price(forwardPrice, args.ShortStrike, args.RiskFreeRate, args.ShortImpliedVolatility, timeToMaturity, optionType);
        double longPrice = OptionModel.Price(forwardPrice, args.LongStrike, args.RiskFreeRate, args.LongImpliedVolatility, timeToMaturity, optionType);

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

    static Black76Result PriceIronCondor(double forwardPrice, double shortCallStrike, double longCallStrike, double shortPutStrike, double longPutStrike, double timeToMaturity, double volatility, double riskFreeRate)
    {
        // Calculate the price of the iron condor spread using the Black-76 model
        // This is a placeholder implementation and should be replaced with actual calculations
        // Calculate the price of each option in the spread
        double shortCallPrice = OptionModel.Price(forwardPrice, shortCallStrike, riskFreeRate, volatility, timeToMaturity, 1 );
        double longCallPrice = OptionModel.Price(forwardPrice, longCallStrike, riskFreeRate, volatility, timeToMaturity, 1 );
        double shortPutPrice = OptionModel.Price(forwardPrice, shortPutStrike, riskFreeRate, volatility, timeToMaturity, -1 );
        double longPutPrice = OptionModel.Price(forwardPrice, longPutStrike, riskFreeRate, volatility, timeToMaturity, -1 );

        // Calculate the total price of the iron condor spread
        double totalPrice = longCallPrice - shortCallPrice + longPutPrice - shortPutPrice;
        return new Black76Result(totalPrice, 0, 0, 0, 0, 0);
    }

}
