using MathNet.Numerics.Distributions;

using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

/// <summary>
/// method extensions for futures option tick data
/// </summary>
public static class TickOptionComputationExtensions
{

    extension(TickOptionComputation optionGreeks)
    {
        public double GetDelta(string optionType, double strikePrice, double timeValue, double riskFreeRate, double dividendYield = 0.0)
        {
            ArgumentNullException.ThrowIfNull(optionGreeks);
            var delta = default(double);
            var underlyingPrice = optionGreeks.UnderlyingPrice;
            var impliedVol = optionGreeks.ImpliedVolatility;
            if (!string.IsNullOrWhiteSpace(optionType))
            {
                switch (optionType.ToUpperInvariant())
                {
                    case OptionTypeName.Call:
                        delta = new Normal().CumulativeDistribution(dOne(underlyingPrice, strikePrice, timeValue, riskFreeRate, impliedVol, dividendYield));
                        break;
                    case OptionTypeName.Put:
                        delta = new Normal().CumulativeDistribution(dOne(underlyingPrice, strikePrice, timeValue, riskFreeRate, impliedVol, dividendYield)) - 1;
                        break;
                }
            }
            return delta;
        }

        public double GetGamma(double strikePrice, double timeValue, double riskFreeRate, double dividendYield = 0.0)
        {
            ArgumentNullException.ThrowIfNull(optionGreeks);
            var underlyingPrice = optionGreeks.UnderlyingPrice;
            var impliedVol = optionGreeks.ImpliedVolatility;
            return NdOne(underlyingPrice, strikePrice, timeValue, riskFreeRate, impliedVol, dividendYield) / (strikePrice * (impliedVol * Math.Sqrt(timeValue)));
        }

        public double SetVega(double strikePrice, double timeValue, double riskFreeRate, double dividendYield = 0.0)
        {
            ArgumentNullException.ThrowIfNull(optionGreeks);
            var underlyingPrice = optionGreeks.UnderlyingPrice;
            var impliedVol = optionGreeks.ImpliedVolatility;
            return 0.01 * underlyingPrice * Math.Sqrt(timeValue)
                * NdOne(underlyingPrice, strikePrice, timeValue, riskFreeRate, impliedVol, dividendYield);
        }

        public double SetTheta(string optionType, double strikePrice, double timeValue, double riskFreeRate, double dividendYield = 0.0)
        {
            ArgumentNullException.ThrowIfNull(optionGreeks);
            var theta = default(double);
            var underlyingPrice = optionGreeks.UnderlyingPrice;
            var impliedVol = optionGreeks.ImpliedVolatility;
            var ndOne = NdOne(underlyingPrice, strikePrice, timeValue, riskFreeRate, impliedVol, dividendYield);
            var ndTwo = NdTwo(underlyingPrice, strikePrice, timeValue, riskFreeRate, impliedVol, dividendYield);
            if (!string.IsNullOrWhiteSpace(optionType))
            {
                switch (optionType)
                {
                    case OptionTypeName.Call:
                        theta = (-(underlyingPrice * impliedVol * ndOne) / (2 * Math.Sqrt(timeValue))
                            - riskFreeRate * strikePrice * Math.Exp(-riskFreeRate * timeValue) * ndTwo) / 365;
                        break;
                    case OptionTypeName.Put:
                        theta = (-(underlyingPrice * impliedVol * ndOne) / (2 * Math.Sqrt(timeValue))
                            + riskFreeRate * strikePrice * Math.Exp(-riskFreeRate * timeValue) * (1 - ndTwo)) / 365;
                        break;
                }
            }
            return theta;
        }

        public double SetRho(string optionType, double strikePrice, double timeValue, double riskFreeRate, double dividendYield = 0.0)
        {
            ArgumentNullException.ThrowIfNull(optionGreeks);
            var rho = default(double);
            var underlyingPrice = optionGreeks.UnderlyingPrice;
            var impliedVol = optionGreeks.ImpliedVolatility;
            if (!string.IsNullOrWhiteSpace(optionType))
            {
                switch (optionType)
                {
                    case OptionTypeName.Call:
                        rho = 0.01 * strikePrice * timeValue * Math.Exp(-riskFreeRate * timeValue) *
                            new Normal().CumulativeDistribution(dOne(underlyingPrice, strikePrice, timeValue, riskFreeRate, impliedVol, dividendYield));
                        break;
                    case OptionTypeName.Put:
                        rho = -0.01 * strikePrice * timeValue * Math.Exp(-riskFreeRate * timeValue) *
                            (1 - new Normal().CumulativeDistribution(dOne(underlyingPrice, strikePrice, timeValue, riskFreeRate, impliedVol, dividendYield)));
                        break;
                }
            }
            return rho;
        }
    }

    private static double dOne(double assetPrice, double strikePrice, double timeValue, double riskFreeRate, double impliedVol, double dividendYield)
        => (Math.Log(assetPrice / strikePrice) + ((riskFreeRate - dividendYield) + 0.5 * impliedVol * impliedVol) * timeValue) / (impliedVol * Math.Sqrt(timeValue));

    private static double NdOne(double assetPrice, double strikePrice, double timeValue, double riskFreeRate, double impliedVol, double dividendYield)
        => Math.Exp(-(Math.Pow(dOne(assetPrice, strikePrice, timeValue, riskFreeRate, impliedVol, dividendYield), 2)) / 2) / Math.Sqrt(2 * Math.PI);

    private static double dTwo(double assetPrice, double strikePrice, double timeValue, double riskFreeRate, double impliedVol, double dividendYield)
        => dOne(assetPrice, strikePrice, timeValue, riskFreeRate, impliedVol, dividendYield) - impliedVol * Math.Sqrt(timeValue);

    private static double NdTwo(double assetPrice, double strikePrice, double timeValue, double riskFreeRate, double impliedVol, double dividendYield)
        => new Normal().CumulativeDistribution(dTwo(assetPrice, strikePrice, timeValue, riskFreeRate, impliedVol, dividendYield));

}
