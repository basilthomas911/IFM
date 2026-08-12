using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using FluentValidation;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class ValidationBenchmarks
{
    static readonly EconomicCalendarValidator CachedValidator = new();
    EconomicCalendarReadModel[] _values = default!;

    [Params(1, 32, 256)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var date = new DateTime(2026, 8, 5);
        _values = Enumerable.Range(0, Rows)
            .Select(i => new EconomicCalendarReadModel(date, "US", $"Event {i}", "1", "1", "1", date, "benchmark"))
            .ToArray();
    }

    [Benchmark(Baseline = true)]
    public int BeforeNewValidatorPerRow()
    {
        var errors = 0;
        foreach (var value in _values)
            errors += new EconomicCalendarValidator().Validate(value).Errors.Count;
        return errors;
    }

    [Benchmark]
    public int AfterCachedValidator()
    {
        var errors = 0;
        foreach (var value in _values)
            errors += CachedValidator.Validate(value).Errors.Count;
        return errors;
    }

    sealed class EconomicCalendarValidator : AbstractValidator<EconomicCalendarReadModel>
    {
        public EconomicCalendarValidator()
        {
            RuleFor(x => x.EventDate).Must(static value => value != DateTime.MinValue && value != DateTime.MaxValue);
            RuleFor(x => x.CountryCode).NotEmpty();
            RuleFor(x => x.EventName).NotEmpty();
        }
    }
}
