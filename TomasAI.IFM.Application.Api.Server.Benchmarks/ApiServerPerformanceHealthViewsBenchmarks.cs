using BenchmarkDotNet.Attributes;
using System.Text.Json;
using TomasAI.IFM.Application.Api.Server.Core.Http.Endpoints;
using TomasAI.IFM.Application.Api.Server.Core.Http.Serialization;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Response serialization only; summary/detail separation intentionally returns less data and keeps compatibility opt-in.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig))]
public class ApiServerPerformanceHealthViewsBenchmarks
{
    SupervisorRuntimeSnapshot snapshot = null!;
    readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
    [GlobalSetup] public void Setup() { snapshot = ActorHealthJsonBenchmarks.CreateSnapshot(); ApiServerJson.Configure(options); }
    [Benchmark(Baseline = true), BenchmarkCategory("08-HealthResponse")]
    public byte[] FullResponseBefore() => JsonSerializer.SerializeToUtf8Bytes(snapshot, options);
    [Benchmark, BenchmarkCategory("08-HealthResponse")]
    public byte[] SummaryResponseAfter() => JsonSerializer.SerializeToUtf8Bytes(ActorHealthReadViews.Summarize(snapshot), options);
}
