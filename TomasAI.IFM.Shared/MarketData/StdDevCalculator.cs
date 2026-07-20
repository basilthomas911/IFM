using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using MathNet.Numerics.Distributions;

namespace TomasAI.IFM.Shared.MarketData;

public class StdDevCalculator
{
    int _windowSize;
    Normal _normDist;

    /// <summary>
    /// create standard deviation calculator
    /// </summary>
    /// <param name="windowSize"></param>
    /// <param name="futuresEodData"></param>
    /// <param name="estimatorFunc"></param>
    public StdDevCalculator(int windowSize, FuturesEodDataV2ReadModel[] futuresEodData, Func<FuturesEodDataV2ReadModel, double> estimatorFunc)
    {
        _windowSize = windowSize;
        _normDist = Normal.Estimate(futuresEodData.Select(estimatorFunc).Take(windowSize));
    }

    public double StdDev => _normDist.StdDev;
    public double StdDevPercent => _normDist.StdDev * Math.Sqrt(_windowSize) / 100;
    public double Mean => _normDist.Mean;

}
