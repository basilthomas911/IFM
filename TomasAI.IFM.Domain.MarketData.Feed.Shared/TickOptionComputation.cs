using MathNet.Numerics.Distributions;

using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

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
