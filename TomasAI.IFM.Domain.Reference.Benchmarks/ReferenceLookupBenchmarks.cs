using System.Collections.Frozen;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace TomasAI.IFM.Domain.Reference.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class ReferenceLookupBenchmarks
{
    Dictionary<string, List<string>> _before = default!;
    FrozenDictionary<string, FrozenSet<string>> _after = default!;
    string _target = string.Empty;

    [Params(32, 512, 4096)]
    public int Entries { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var values = Enumerable.Range(0, Entries).Select(static i => $"CODE{i}").ToList();
        _target = $"code{Entries - 1}";
        _before = new Dictionary<string, List<string>> { ["Currency"] = values };
        _after = new Dictionary<string, FrozenSet<string>>
        {
            ["Currency"] = values.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
        }.ToFrozenDictionary(StringComparer.Ordinal);
    }

    [Benchmark(Baseline = true)]
    public bool BeforeDictionaryListScan()
        => _before.ContainsKey("Currency")
            && _before["Currency"].Any(value => value.Equals(_target, StringComparison.CurrentCultureIgnoreCase));

    [Benchmark]
    public bool AfterFrozenIndex()
        => _after.TryGetValue("Currency", out var values) && values.Contains(_target);
}
