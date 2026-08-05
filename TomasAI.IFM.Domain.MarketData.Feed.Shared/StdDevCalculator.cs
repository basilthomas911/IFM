using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

/// <summary>
/// Calculates sample statistics for a bounded EOD window without iterator or distribution allocations.
/// </summary>
public sealed class StdDevCalculator
{
    readonly int _windowSize;
    readonly double _stdDev;
    readonly double _mean;

    public StdDevCalculator(
        int windowSize,
        FuturesEodDataV2ReadModel[] futuresEodData,
        Func<FuturesEodDataV2ReadModel, double> estimatorFunc)
    {
        ArgumentNullException.ThrowIfNull(futuresEodData);
        ArgumentNullException.ThrowIfNull(estimatorFunc);
        if (windowSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(windowSize));

        _windowSize = windowSize;
        var count = Math.Min(windowSize, futuresEodData.Length);
        var mean = 0.0;
        var sumOfSquaredDifferences = 0.0;

        for (var index = 0; index < count; index++)
        {
            var value = estimatorFunc(futuresEodData[index]);
            var delta = value - mean;
            mean += delta / (index + 1);
            sumOfSquaredDifferences += delta * (value - mean);
        }

        _mean = mean;
        _stdDev = count > 1
            ? Math.Sqrt(sumOfSquaredDifferences / (count - 1))
            : 0.0;
    }

    public double StdDev => _stdDev;
    public double StdDevPercent => _stdDev * Math.Sqrt(_windowSize) / 100;
    public double Mean => _mean;
}
