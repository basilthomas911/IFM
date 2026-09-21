using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Framework.OptionPricer.Black76;
using TomasAI.IFM.Framework.OptionPricer.Pricing;

namespace TomasAI.IFM.Domain.OptionPricer.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class OptionCalculatorBenchmarks
{
    private readonly OptionCalculator _calculator = new();
    private readonly double _callMarketPrice = OptionModel.Price(5_200, 5_250, 0.045, 0.22, 1.0, 1);
    private readonly double _putMarketPrice = OptionModel.Price(5_200, 5_150, 0.045, 0.24, 1.0, -1);
    private readonly double _longCallMarketPrice = OptionModel.Price(5_200, 5_300, 0.045, 0.21, 1.0, 1);
    private readonly double _longPutMarketPrice = OptionModel.Price(5_200, 5_100, 0.045, 0.23, 1.0, -1);

    [Benchmark]
    public PricingResult CalculateCall() => _calculator.ImpliedVolatility(
        Request(OptionSide.Call, 5_250), _callMarketPrice);

    [Benchmark(OperationsPerInvoke = 4)]
    public double CalculateFourLegs()
    {
        var shortPut = _calculator.ImpliedVolatility(Request(OptionSide.Put, 5_150), _putMarketPrice);
        var longPut = _calculator.ImpliedVolatility(Request(OptionSide.Put, 5_100), _longPutMarketPrice);
        var shortCall = _calculator.ImpliedVolatility(Request(OptionSide.Call, 5_250), _callMarketPrice);
        var longCall = _calculator.ImpliedVolatility(Request(OptionSide.Call, 5_300), _longCallMarketPrice);

        return shortPut.Value!.Value.Delta + longPut.Value!.Value.Delta +
            shortCall.Value!.Value.Delta + longCall.Value!.Value.Delta;
    }

    private static OptionPricingRequest Request(OptionSide side, double strike) =>
        new(UnderlyingKind.Futures, ExerciseKind.European, PremiumKind.PaidUpfront,
            side, 5_200, strike, 1, .045);
}
