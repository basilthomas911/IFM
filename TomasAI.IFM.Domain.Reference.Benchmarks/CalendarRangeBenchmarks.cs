using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.Reference.EconomicCalendar.Query;

namespace TomasAI.IFM.Domain.Reference.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class CalendarRangeBenchmarks
{
    DateTime _date;

    [Params(0, 3, 6)]
    public int DaysAfterSunday { get; set; }

    [GlobalSetup]
    public void Setup() => _date = new DateTime(2026, 8, 2).AddDays(DaysAfterSunday);

    [Benchmark(Baseline = true)]
    public DateTime BeforeLoop()
    {
        var result = _date;
        while (result.DayOfWeek != DayOfWeek.Monday)
            result = result.AddDays(-1);
        return result.Date;
    }

    [Benchmark]
    public DateTime AfterArithmetic() => _date.GetThisWeekStartingDate();
}
