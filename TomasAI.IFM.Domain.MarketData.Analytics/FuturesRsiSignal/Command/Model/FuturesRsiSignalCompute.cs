using TomasAI.IFM.Shared.AlgoMath.Indicators;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;

internal static class FuturesRsiSignalCompute
{
    public static FuturesRsiSignalReadModel SetRsiSignalAtWindowSize(this FuturesRsiSignalReadModel newRSISignal, IReadOnlyCollection<FuturesRsiSignalReadModel> rsiSignals, int rsiWindowSize)
    {
        var prevIndex = rsiSignals.Count - 1;
        var priceChange = newRSISignal.Price - rsiSignals.ElementAt(prevIndex).Price;
        newRSISignal = newRSISignal with
        {
            PriceChange = priceChange,
            PriceGain = priceChange > 0 ? priceChange : 0m,
            PriceLoss = priceChange < 0 ? Math.Abs(priceChange) : 0m
        };
        var avgPriceGain = (rsiSignals.Skip(1).Average(e => e.PriceGain) * (rsiWindowSize - 1) + newRSISignal.PriceGain) / rsiWindowSize;
        var avgPriceLoss = (rsiSignals.Skip(1).Average(e => e.PriceLoss) * (rsiWindowSize - 1) + newRSISignal.PriceLoss) / rsiWindowSize;
        var rs = Convert.ToDouble(avgPriceGain / (avgPriceLoss == 0.0m ? 1.0m : avgPriceLoss));
        var rsi = 100 - (100 / (1 + rs));
        var xmaInd = new ExponentialMovingAverageIndicator(6);
        foreach (var e in rsiSignals)
            xmaInd.Estimate(e.RSI);
        var rsiAvg = xmaInd.Value;
        var rsiSlope = Convert.ToDouble(rsiSignals.Last().Price - rsiSignals.First().Price) / Convert.ToDouble(rsiWindowSize);
        newRSISignal = newRSISignal with
        {
            AveragePriceGain = avgPriceGain,
            AveragePriceLoss = avgPriceLoss,
            RS = rs,
            RSI = rsi,
            RSIAverage = rsiAvg,
            RSISlope = rsiSlope
        };
        return newRSISignal;
    }

    /// <summary>
    /// Determines whether futures RSI signals can be generated based on the specified window size.
    /// </summary>
    /// <param name="futuresRsiSignals">A read-only collection of futures RSI signal view models to evaluate. Each signal should have a valid RSI value to be considered for generation.</param>
    /// <param name="periodLength">The number of valid signals required to generate futures RSI signals. Must be a non-negative integer.</param>
    /// <returns><see langword="true"/> if there are valid RSI signals within the window; otherwise, <see langword="false"/>.</returns>
    internal static bool CanGenerateFuturesRsiSignals(this IReadOnlyCollection<FuturesRsiSignalReadModel> futuresRsiSignals, int periodLength)
        => futuresRsiSignals.Where(o => o.RSI != -1).Take(periodLength).Any();

    /// <summary>
    /// Generates a collection of futures RSI signal view models that have valid RSI values, limited to the specified
    /// window size.
    /// </summary>
    /// <remarks>Signals with an RSI value of -1 are considered invalid and are not included in the result.
    /// The returned collection will contain at most the number of items specified by the window size
    /// parameter.</remarks>
    /// <param name="futuresRsiSignals">A read-only collection of futures RSI signal view models to filter and process. Each signal should have a valid
    /// RSI value to be included in the result.</param>
    /// <param name="windowSize">The maximum number of valid signals to include in the returned collection. Must be a non-negative integer.</param>
    /// <returns>A read-only collection containing up to the specified number of futures RSI signal view models with valid RSI
    /// values. Signals with an RSI value of -1 are excluded.</returns>
    internal static IReadOnlyCollection<FuturesRsiSignalReadModel> GenerateFuturesRsiSignals(this IReadOnlyCollection<FuturesRsiSignalReadModel> futuresRsiSignals, int windowSize)
        => [.. futuresRsiSignals.Where(o => o.RSI != -1).Take(windowSize)];

    /// <summary>
    /// Generates a new RSI signal based on the provided end-of-day data and the existing collection of RSI signals.
    /// </summary>
    /// <param name="rsiSignals">The collection of existing RSI signals.</param>
    /// <param name="futuresEodData">The end-of-day data for the futures contract.</param>
    /// <param name="rsiPeriodLength">The period length for calculating the RSI.</param>
    /// <returns>The generated RSI signal.</returns>
    internal static FuturesRsiSignalReadModel GenerateRsiSignal(
        this IReadOnlyCollection<FuturesRsiSignalReadModel> rsiSignals,
        FuturesRsiSignalId signalId,
        decimal futuresPrice)
    {
        var rsiSignal = CreateRsiSignal(signalId, futuresPrice);
        return rsiSignals.Count switch
        {
            0 => rsiSignal,
            _ when rsiSignals.Count < signalId.PeriodLength
                => SetRsiSignalPreWindowSize(rsiSignal, rsiSignals),
            _ => rsiSignal.SetRsiSignalAtWindowSize(rsiSignals, signalId.PeriodLength)
        };
    }

    /// <summary>
    /// Creates an initial RSI signal view model with default values from end-of-day data.
    /// </summary>
    internal static FuturesRsiSignalReadModel CreateRsiSignal(this FuturesRsiSignalId signalId, decimal futuresPrice)
        => new(
            contractId: signalId.ContractId,
            valueDate: signalId.ValueDate,
            timePeriod: signalId.TimePeriod,
            periodLength: signalId.PeriodLength,
            timestamp: TimeOnly.FromDateTime(DateTime.Now),
            price: futuresPrice,
            priceChange: 0m,
            priceGain: 0m,
            priceLoss: 0m,
            averagePriceGain: 0m,
            averagePriceLoss: 0m,
            rs: 0.0,
            rsi: -1,
            rsiAverage: 0.0,
            rsiSlope: 0.0);

    /// <summary>
    /// Sets the price change, gain, and loss for a new RSI signal based on the previous signal in the collection.
    /// </summary>
    /// <param name="newRsiSignal">The new RSI signal to update.</param>
    /// <param name="rsiSignals">The collection of existing RSI signals.</param>
    /// <returns>The updated RSI signal.</returns>
    public static FuturesRsiSignalReadModel SetRsiSignalPreWindowSize(this FuturesRsiSignalReadModel newRsiSignal, IReadOnlyCollection<FuturesRsiSignalReadModel> rsiSignals)
    {
        var prevIndex = rsiSignals.Count - 1;
        var priceChange = newRsiSignal.Price - rsiSignals.ElementAt(prevIndex).Price;
        return newRsiSignal with
        {
            PriceChange = priceChange,
            PriceGain = priceChange > 0 ? priceChange : 0m,
            PriceLoss = priceChange < 0 ? Math.Abs(priceChange) : 0m
        };
    }


}
