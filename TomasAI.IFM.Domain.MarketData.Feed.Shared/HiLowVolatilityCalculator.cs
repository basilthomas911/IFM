namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

/// <summary>
/// Calculates high/low volatility and its exponential moving average without transient collections.
/// </summary>
public sealed class HiLowVolatilityCalculator
{
    readonly double _expMovAvg;
    readonly double _hiLowVolatility;

    public HiLowVolatilityCalculator(int windowSize, IEodBarData[] eodBarData)
    {
        ArgumentNullException.ThrowIfNull(eodBarData);
        if (windowSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(windowSize));
        if (eodBarData.Length < windowSize * 2)
            throw new ArgumentException("Two complete windows are required.", nameof(eodBarData));

        _hiLowVolatility = GetHiLow(eodBarData[0]);

        var seed = 0.0;
        for (var index = windowSize; index < windowSize * 2; index++)
            seed += GetHiLow(eodBarData[index]);

        var ema = seed / windowSize;
        var multiplier = 2.0 / (windowSize + 1.0);
        for (var index = windowSize - 1; index >= 0; index--)
            ema = GetHiLow(eodBarData[index]) * multiplier + ema * (1.0 - multiplier);

        _expMovAvg = ema;
    }

    public double ExpMovAvg => _expMovAvg;
    public double HiLowVolatility => _hiLowVolatility;

    static double GetHiLow(IEodBarData data)
        => (data.HighPrice - data.LowPrice) / (0.01 * data.ClosePrice);
}
