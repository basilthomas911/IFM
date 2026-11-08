using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Both cases connect to the same disposable Redis and wait for readiness; asynchronous startup does not eliminate network work.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig))]
public class ApiServerPerformanceRedisBenchmarks
{
    readonly ApiServerPerformanceDatabaseFixture database = new();
    [GlobalSetup] public Task Setup() => database.InitializeAsync();

    [Benchmark(Baseline = true), BenchmarkCategory("20-RedisConnectionReady")]
    public void SynchronousBefore()
    {
        using var connection = ConnectionMultiplexer.Connect(database.Redis);
        if (!connection.IsConnected) throw new InvalidOperationException("Redis did not connect.");
    }

    [Benchmark, BenchmarkCategory("20-RedisConnectionReady")]
    public async Task AsynchronousAfter()
    {
        await using var startup = new RedisConnectionStartup(database.Redis, NullLogger<RedisConnectionStartup>.Instance);
        await startup.StartAsync(default);
        if (!(await startup.GetAsync(default)).IsConnected) throw new InvalidOperationException("Redis did not connect.");
    }

    [GlobalCleanup] public async Task Cleanup() => await database.DisposeAsync();
}
