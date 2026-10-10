using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Domain.MarketData.Query;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.Util;

namespace TomasAI.IFM.Domain.MarketData.Benchmarks;

/// <summary>Measures production scope coverage and versioned metadata reuse without attributing synthetic I/O to Databento.</summary>
[MemoryDiagnoser]
public class OptionChainLatencyBenchmarks
{
    [Params(160, 512)] public int Contracts { get; set; }
    FuturesOptionContractReadModel[] definitions = [];
    FuturesOptionContractReadModel[] requested = [];
    Dictionary<string, (string MappingVersion, string DefinitionDigest)> identities = new();
    AsyncReadCache<string, string> cache = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        definitions = Enumerable.Range(0, Contracts).Select(i => new FuturesOptionContractReadModel(
            "option-" + i, "", "ES", "", "OPT", "USD", "CME", "50", new(2026, 12, 1), 4800 + i * 5,
            i % 2 == 0 ? "C" : "P") { MappingVersion = "v1", DefinitionDigest = "d1" }).ToArray();
        requested = definitions.Skip(Contracts / 4).Take(Contracts / 2).ToArray();
        identities = definitions.ToDictionary(x => x.ContractId, x => (x.MappingVersion!, x.DefinitionDigest!));
        cache = new(8192, TimeSpan.FromHours(1));
        await cache.GetAsync("v1", Read);
    }
    static Task<string?> Read(CancellationToken _) => Task.FromResult<string?>("immutable-reference");

    [Benchmark(Baseline = true)]
    public bool ExactSortedScopeComparison() => identities.Keys.Order(StringComparer.Ordinal)
        .SequenceEqual(requested.Select(x => x.ContractId).Order(StringComparer.Ordinal));

    [Benchmark]
    public bool BufferedScopeReuse() => OptionChainSubscriptionCoverage.Covers(identities, requested);

    [Benchmark]
    public FuturesOptionContractReadModel[] BuildBufferedScope() => OptionChainSubscriptionCoverage.Buffer(definitions,
        OptionChainStrikeWindow.Select(definitions, (decimal)requested[requested.Length / 2].StrikePrice, 40, 2.5), 50);

    [Benchmark]
    public Task<string?> PublishedReferenceCacheHit() => cache.GetAsync("v1", Read);
}
