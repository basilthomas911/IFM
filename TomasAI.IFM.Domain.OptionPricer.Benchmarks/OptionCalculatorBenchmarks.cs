using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Framework.OptionPricer.Black76;

namespace TomasAI.IFM.Domain.OptionPricer.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class OptionCalculatorBenchmarks
{
    private readonly OptionCalculator _calculator = new(
        new DateOnly(2026, 1, 1),
        new DateOnly(2027, 1, 1));
    private readonly double _callMarketPrice = OptionModel.Price(5_200, 5_250, 0.045, 0.22, 1.0, 1);
    private readonly double _putMarketPrice = OptionModel.Price(5_200, 5_150, 0.045, 0.24, 1.0, -1);
    private readonly double _longCallMarketPrice = OptionModel.Price(5_200, 5_300, 0.045, 0.21, 1.0, 1);
    private readonly double _longPutMarketPrice = OptionModel.Price(5_200, 5_100, 0.045, 0.23, 1.0, -1);

    [Benchmark]
    public OptionGreeks CalculateCall() => _calculator.GetOptionGreeks(
        OptionTypeName.Call,
        5_200,
        5_250,
        _callMarketPrice,
        0.045);

    [Benchmark(OperationsPerInvoke = 4)]
    public double CalculateFourLegs()
    {
        var shortPut = _calculator.GetOptionGreeks(
            OptionTypeName.Put, 5_200, 5_150, _putMarketPrice, 0.045);
        var longPut = _calculator.GetOptionGreeks(
            OptionTypeName.Put, 5_200, 5_100, _longPutMarketPrice, 0.045);
        var shortCall = _calculator.GetOptionGreeks(
            OptionTypeName.Call, 5_200, 5_250, _callMarketPrice, 0.045);
        var longCall = _calculator.GetOptionGreeks(
            OptionTypeName.Call, 5_200, 5_300, _longCallMarketPrice, 0.045);

        return shortPut.Delta + longPut.Delta + shortCall.Delta + longCall.Delta;
    }
}
