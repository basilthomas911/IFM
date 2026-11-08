using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using TomasAI.IFM.Application.Api.Server.Benchmarks.Baselines;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Remote;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Identical client-operation and wire fixtures for the November baseline and production codec.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[Config(typeof(ApiServerPerformanceConfig))]
public class ApiServerPerformanceRpcBenchmarks
{
    readonly Type[] types = [typeof(string[])];
    object?[] arguments = null!;

    [Params(13, 218, 3640, 50000)]
    public int Contracts { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        arguments = [Enumerable.Range(0, Contracts).Select(index => $"ES20261218-{index:D6}").ToArray()];
        _ = ApiServerPerformanceRpcBaseline.Encode(nameof(IMarketDataApi.GetFuturesContractsAsync), types, arguments);
        if (!ApiServerPerformanceRpcBaseline.Encode(nameof(IMarketDataApi.GetFuturesContractsAsync), types, arguments)
            .AsSpan().SequenceEqual(MarketDataApiCallCodec.Encode(nameof(IMarketDataApi.GetFuturesContractsAsync), types, arguments)))
            throw new InvalidOperationException("RPC wire fixtures differ.");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("04-RpcWirePayload")]
    public byte[] PayloadBefore() => ApiServerPerformanceRpcBaseline.Encode(nameof(IMarketDataApi.GetFuturesContractsAsync), types, arguments);

    [Benchmark, BenchmarkCategory("04-RpcWirePayload")]
    public byte[] PayloadAfter() => MarketDataApiCallCodec.Encode(nameof(IMarketDataApi.GetFuturesContractsAsync), types, arguments);
}

/// <summary>Warmed descriptor lookup without a payload-size Cartesian product.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, CategoriesColumn]
[Config(typeof(ApiServerPerformanceConfig))]
public class ApiServerPerformanceRpcDescriptorBenchmarks
{
    readonly Type[] types = [typeof(string[])];

    [Benchmark(Baseline = true), BenchmarkCategory("03-OperationDescriptor")]
    public string DescriptorBefore() => ApiServerPerformanceRpcBaseline.Signature(nameof(IMarketDataApi.GetFuturesContractsAsync), types);

    [Benchmark, BenchmarkCategory("03-OperationDescriptor")]
    public string DescriptorAfter() => MarketDataOperationCatalog.Shared.Resolve(nameof(IMarketDataApi.GetFuturesContractsAsync), types).Signature;
}
