using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class AtrIndicatorBenchmarks : IndicatorBenchmarkBase
{
    [Benchmark(Baseline = true)]
    public IndicatorResult Before() => LegacyIndicators.ComputeAtr(AtrSignals, Period);

    [Benchmark]
    public IndicatorResult After()
    {
        FuturesAtrSignalCompute.Create(Period, null, AtrSignals, out var model);
        return new IndicatorResult(model.AtrValue, model.TrueRange, 0);
    }
}

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class AdxIndicatorBenchmarks : IndicatorBenchmarkBase
{
    [Benchmark(Baseline = true)]
    public IndicatorResult Before() => LegacyIndicators.ComputeAdx(AdxSignals, Period);

    [Benchmark]
    public IndicatorResult After()
    {
        FuturesAdxSignalCompute.Create(Period, null, AdxSignals, out var model);
        return new IndicatorResult(model.PlusDI, model.MinusDI, model.AdxValue);
    }
}

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class MacdIndicatorBenchmarks : IndicatorBenchmarkBase
{
    [Benchmark(Baseline = true)]
    public IndicatorResult Before() => LegacyIndicators.ComputeMacd(MacdSignals, Period);

    [Benchmark]
    public IndicatorResult After()
    {
        FuturesMacdSignalCompute.Create(Period, MacdSignals, out var model);
        return new IndicatorResult(model.MacdLine, model.SignalLine, model.Histogram);
    }
}

public abstract class IndicatorBenchmarkBase
{
    protected const int Period = 14;
    protected FuturesAtrSignalReadModel[] AtrSignals { get; private set; } = null!;
    protected FuturesAdxSignalReadModel[] AdxSignals { get; private set; } = null!;
    protected FuturesMacdSignalReadModel[] MacdSignals { get; private set; } = null!;

    [Params(32, 256, 2048)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        AtrSignals = new FuturesAtrSignalReadModel[Count];
        AdxSignals = new FuturesAdxSignalReadModel[Count];
        MacdSignals = new FuturesMacdSignalReadModel[Count];
        var valueDate = new DateOnly(2026, 8, 5);
        for (var index = 0; index < Count; index++)
        {
            var price = 5400m + (decimal)(Math.Sin(index * 0.17) * 20) + index * 0.01m;
            var timestamp = TimeOnly.FromTimeSpan(TimeSpan.FromSeconds(index));
            AtrSignals[index] = new("ESU26", valueDate, TimeFrameType.Daily, Period, timestamp, price, 0, 0,
                FuturesTrendDirectionType.Init, FuturesTrendDirectionStrengthType.Low);
            AdxSignals[index] = new("ESU26", valueDate, TimeFrameType.Daily, Period, timestamp, price, 0, 0, 0,
                FuturesTrendDirectionType.Init, FuturesTrendDirectionStrengthType.Low);
            MacdSignals[index] = new("ESU26", valueDate, TimeFrameType.Daily, Period, timestamp, price, 0, 0, 0,
                FuturesTrendDirectionType.Init, FuturesTrendDirectionStrengthType.Low);
        }
    }
}

public readonly record struct IndicatorResult(double First, double Second, double Third);

static class LegacyIndicators
{
    public static IndicatorResult ComputeAtr(IReadOnlyCollection<FuturesAtrSignalReadModel> signals, int period)
    {
        var prices = signals.Select(static signal => (double)signal.FuturesPrice).ToArray();
        if (prices.Length < 2)
            return default;
        var ranges = new double[prices.Length - 1];
        for (var index = 1; index < prices.Length; index++)
            ranges[index - 1] = Math.Abs(prices[index] - prices[index - 1]);
        var atr = ranges.Length <= period ? ranges.Average() : ranges.Take(period).Average();
        for (var index = period; index < ranges.Length; index++)
            atr = ((atr * (period - 1)) + ranges[index]) / period;
        return new IndicatorResult(atr, ranges[^1], 0);
    }

    public static IndicatorResult ComputeAdx(IReadOnlyCollection<FuturesAdxSignalReadModel> signals, int period)
    {
        var prices = signals.Select(static signal => (double)signal.FuturesPrice).ToArray();
        if (prices.Length < 2)
            return default;
        var plusDm = new double[prices.Length - 1];
        var minusDm = new double[prices.Length - 1];
        var ranges = new double[prices.Length - 1];
        for (var index = 1; index < prices.Length; index++)
        {
            var upMove = prices[index] - prices[index - 1];
            var downMove = prices[index - 1] - prices[index];
            plusDm[index - 1] = upMove > 0 && upMove > downMove ? upMove : 0;
            minusDm[index - 1] = downMove > 0 && downMove > upMove ? downMove : 0;
            ranges[index - 1] = Math.Abs(upMove);
        }
        var smoothedTr = WilderSmooth(ranges, period);
        var smoothedPlusDm = WilderSmooth(plusDm, period);
        var smoothedMinusDm = WilderSmooth(minusDm, period);
        if (smoothedTr == 0)
            return default;
        var plusDi = smoothedPlusDm / smoothedTr * 100;
        var minusDi = smoothedMinusDm / smoothedTr * 100;
        var sum = plusDi + minusDi;
        var adx = sum == 0 ? 0 : Math.Abs(plusDi - minusDi) / sum * 100;
        return new IndicatorResult(plusDi, minusDi, adx);
    }

    public static IndicatorResult ComputeMacd(IReadOnlyCollection<FuturesMacdSignalReadModel> signals, int signalPeriod)
    {
        const int fastPeriod = 9;
        const int slowPeriod = 26;
        var prices = signals.Select(static signal => (double)signal.FuturesPrice).ToArray();
        var fastEma = ComputeEma(prices, fastPeriod);
        var slowEma = ComputeEma(prices, slowPeriod);
        var macd = fastEma - slowEma;
        var series = new double[prices.Length];
        if (prices.Length > 0)
        {
            var fast = prices[0];
            var slow = prices[0];
            var fastMultiplier = 2d / (fastPeriod + 1);
            var slowMultiplier = 2d / (slowPeriod + 1);
            for (var index = 1; index < prices.Length; index++)
            {
                fast = (prices[index] - fast) * fastMultiplier + fast;
                slow = (prices[index] - slow) * slowMultiplier + slow;
                series[index] = fast - slow;
            }
        }
        var signal = ComputeEma(series, signalPeriod);
        return new IndicatorResult(macd, signal, macd - signal);
    }

    static double WilderSmooth(double[] values, int period)
    {
        var smoothed = values.Length <= period ? values.Average() : values.Take(period).Average();
        for (var index = period; index < values.Length; index++)
            smoothed = ((smoothed * (period - 1)) + values[index]) / period;
        return smoothed;
    }

    static double ComputeEma(double[] values, int period)
    {
        if (values.Length == 0)
            return 0;
        var multiplier = 2d / (period + 1);
        var ema = values[0];
        for (var index = 1; index < values.Length; index++)
            ema = (values[index] - ema) * multiplier + ema;
        return ema;
    }
}
