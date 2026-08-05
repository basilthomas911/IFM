using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using FluentValidation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class ValidationBenchmarks
{
    readonly FuturesRsiSignalEntityIdValidationRules _rules = new();
    FuturesRsiSignalEntityId[] _ids = null!;

    [Params(1, 32, 256)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
        => _ids = Enumerable.Range(0, Count)
            .Select(index => new FuturesRsiSignalEntityId(
                $"ES{index}", new DateOnly(2026, 8, 5), TimeFrameType.Daily, 14))
            .ToArray();

    [Benchmark(Baseline = true)]
    public int Before()
    {
        var errors = 0;
        foreach (var id in _ids)
            errors += new LegacyValidator().Validate(id).Errors.Count;
        return errors;
    }

    [Benchmark]
    public int After()
    {
        var errors = 0;
        foreach (var id in _ids)
            errors += _rules.Execute(id).Length;
        return errors;
    }

    sealed class LegacyValidator : AbstractValidator<FuturesRsiSignalEntityId>
    {
        public LegacyValidator()
        {
            RuleFor(static id => id.ContractId).NotEmpty();
            RuleFor(static id => id.ValueDate).LessThan(DateOnly.MaxValue);
            RuleFor(static id => id.ValueDate).GreaterThan(DateOnly.MinValue);
            RuleFor(static id => id.PeriodLength).GreaterThan(0);
            RuleFor(static id => id.TimePeriod).IsInEnum().NotEqual(TimeFrameType.None);
        }
    }
}
