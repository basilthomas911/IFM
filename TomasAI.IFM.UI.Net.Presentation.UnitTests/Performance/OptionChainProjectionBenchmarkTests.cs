using System.Diagnostics;
using Xunit.Abstractions;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Performance;

public sealed class OptionChainProjectionBenchmarkTests(ITestOutputHelper output)
{
    private readonly record struct Contract(int Strike, bool IsCall, decimal Bid);
    private readonly record struct Row(int Strike, Contract? Call, Contract? Put);

    [Fact]
    public void Baseline_group_and_sort_eighty_strikes()
    {
        var contracts = Enumerable.Range(0, 80)
            .SelectMany(strike => new[] { new Contract(5000 + strike * 5, true, strike),
                new Contract(5000 + strike * 5, false, strike) }).ToArray();
        Assert.Equal(ProjectBaseline(contracts), ProjectOptimized(contracts));
        const int iterations = 10_000;
        for (var index = 0; index < 100; index++) ProjectBaseline(contracts);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        var checksum = 0;
        for (var index = 0; index < iterations; index++) checksum += ProjectBaseline(contracts).Count;
        watch.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(iterations * 80, checksum);
        output.WriteLine($"baseline: {watch.Elapsed.TotalMilliseconds / iterations:F4} ms/chain, {allocated / iterations:N0} bytes/chain");
    }

    [Fact]
    public void Optimized_index_and_sort_eighty_strikes()
    {
        var contracts = Enumerable.Range(0, 80)
            .SelectMany(strike => new[] { new Contract(5000 + strike * 5, true, strike),
                new Contract(5000 + strike * 5, false, strike) }).ToArray();
        const int iterations = 10_000;
        for (var index = 0; index < 100; index++) ProjectOptimized(contracts);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        var checksum = 0;
        for (var index = 0; index < iterations; index++) checksum += ProjectOptimized(contracts).Count;
        watch.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(iterations * 80, checksum);
        output.WriteLine($"optimized: {watch.Elapsed.TotalMilliseconds / iterations:F4} ms/chain, {allocated / iterations:N0} bytes/chain");
    }

    private static List<Row> ProjectBaseline(Contract[] contracts)
    {
        var rows = new List<Row>();
        foreach (var group in contracts.GroupBy(x => x.Strike).OrderByDescending(x => x.Key))
            rows.Add(new(group.Key, group.FirstOrDefault(x => x.IsCall),
                group.FirstOrDefault(x => !x.IsCall)));
        return rows;
    }

    private static List<Row> ProjectOptimized(Contract[] contracts)
    {
        var byStrike = new Dictionary<int, (Contract? Call, Contract? Put)>();
        foreach (var contract in contracts)
        {
            byStrike.TryGetValue(contract.Strike, out var pair);
            if (contract.IsCall) pair.Call = contract;
            else pair.Put = contract;
            byStrike[contract.Strike] = pair;
        }
        var strikes = byStrike.Keys.ToArray();
        Array.Sort(strikes);
        var rows = new List<Row>(strikes.Length);
        for (var index = strikes.Length - 1; index >= 0; index--)
        {
            var strike = strikes[index];
            var pair = byStrike[strike];
            rows.Add(new(strike, pair.Call, pair.Put));
        }
        return rows;
    }
}
