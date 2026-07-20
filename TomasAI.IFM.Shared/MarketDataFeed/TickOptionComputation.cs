using MathNet.Numerics.Distributions;

namespace TomasAI.IFM.Shared.MarketDataFeed;

public class TickOptionComputation
{
    private readonly string _optCompType;
    private readonly double _impliedVol;
    private readonly double _delta;
    private readonly double _optPrice;
    private readonly double _pvDividend;
    private readonly double _gamma;
    private readonly double _vega;
    private readonly double _theta;
    private readonly double _rho;
    private readonly double _undPrice;

    public string ComputationType => _optCompType;
    public double ImpliedVolatility => _impliedVol;
    public double Delta => _delta;
    public double OptionPrice => _optPrice;
    public double PVDividend => _pvDividend;
    public double Gamma => _gamma;
    public double Vega => _vega;
    public double Theta => _theta;
    public double Rho => _rho;
    public double UnderlyingPrice => _undPrice;

    public TickOptionComputation(
        string optCompType,
        double impliedVol,
        double delta,
        double optPrice,
        double pvDividend,
        double gamma,
        double vega,
        double theta,
        double rho,
        double undPrice)
    {
        _optCompType = optCompType;
        _impliedVol = impliedVol;
        _delta = delta;
        _optPrice = optPrice;
        _pvDividend = pvDividend;
        _gamma = gamma;
        _vega = vega;
        _theta = theta;
        _rho = rho;
        _undPrice = undPrice;
    }
}

/// <summary>
/// method extensions for futures option tick data
/// </summary>
public static class TickOptionComputationExtensions
{
    public static double GetDelta(this TickOptionComputation optionGreeks,
        string optionType, double strikePrice, double timeValue, double riskFreeRate, double dividendYield = 0.0)
    {
        var delta = default(double);
        var underlyingPrice = optionGreeks.UnderlyingPrice;
        var impliedVol = optionGreeks.ImpliedVolatility;
        if (!string.IsNullOrWhiteSpace(optionType))
        {
            switch (optionType.ToUpper())
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

    public static double GetGamma(this TickOptionComputation optionGreeks, 
        double strikePrice, double timeValue, double riskFreeRate, double dividendYield = 0.0)
    {
        var underlyingPrice = optionGreeks.UnderlyingPrice;
        var impliedVol = optionGreeks.ImpliedVolatility;
        return NdOne(underlyingPrice, strikePrice, timeValue, riskFreeRate, impliedVol, dividendYield) / (strikePrice * (impliedVol * Math.Sqrt(timeValue)));
    }

    public static double SetVega(this TickOptionComputation optionGreeks, 
        double strikePrice, double timeValue, double riskFreeRate, double dividendYield = 0.0)
    {
        var underlyingPrice = optionGreeks.UnderlyingPrice;
        var impliedVol = optionGreeks.ImpliedVolatility;
        return 0.01 * underlyingPrice * Math.Sqrt(timeValue)
            * NdOne(underlyingPrice, strikePrice, timeValue, riskFreeRate, impliedVol, dividendYield);
    }

    public static double SetTheta(this TickOptionComputation optionGreeks, 
        string optionType, double strikePrice, double timeValue, double riskFreeRate, double dividendYield = 0.0)
    {
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

    public static double SetRho(this TickOptionComputation optionGreeks, 
        string optionType, double strikePrice, double timeValue, double riskFreeRate, double dividendYield = 0.0)
    {
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

    private static double dOne(double assetPrice, double strikePrice, double timeValue, double riskFreeRate, double impliedVol, double dividendYield)
        => (Math.Log(assetPrice / strikePrice) + ((riskFreeRate - dividendYield) + 0.5 * impliedVol * impliedVol) * timeValue) / (impliedVol * Math.Sqrt(timeValue));

    private static double NdOne(double assetPrice, double strikePrice, double timeValue, double riskFreeRate, double impliedVol, double dividendYield)
        => Math.Exp(-(Math.Pow(dOne(assetPrice, strikePrice, timeValue, riskFreeRate, impliedVol, dividendYield), 2)) / 2) / Math.Sqrt(2 * Math.PI);

    private static double dTwo(double assetPrice, double strikePrice, double timeValue, double riskFreeRate, double impliedVol, double dividendYield)
        => dOne(assetPrice, strikePrice, timeValue, riskFreeRate, impliedVol, dividendYield) - impliedVol * Math.Sqrt(timeValue);

    private static double NdTwo(double assetPrice, double strikePrice, double timeValue, double riskFreeRate, double impliedVol, double dividendYield)
        => new Normal().CumulativeDistribution(dTwo(assetPrice, strikePrice, timeValue, riskFreeRate, impliedVol, dividendYield));

}
