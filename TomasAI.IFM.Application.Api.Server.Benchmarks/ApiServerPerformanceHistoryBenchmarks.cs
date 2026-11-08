using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TomasAI.IFM.Application.Api.Server.Benchmarks.Baselines;
using TomasAI.IFM.Application.Api.Server.Core.Observability.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Includes the complete warmed persistence read path and independently owned returned rows.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig))]
public class ApiServerPerformanceHistoryBenchmarks
{
    [Params(1000, 10080)] public int Rows { get; set; }
    string root = null!;
    ApiServerPerformanceHistoryBaseline before = null!;
    SupervisorFileHistoryPersistence after = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        root = Path.Combine("E:\\IFM\\BenchmarkArtifacts\\ApiServerPerformance\\20261107", "history-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Logs"));
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        await File.WriteAllLinesAsync(Path.Combine(root, "Logs", "actor-health-history.jsonl"), Enumerable.Range(0, Rows).Select(index =>
            System.Text.Json.JsonSerializer.Serialize(new SupervisorHealthHistoryPoint(index, DateTime.UnixEpoch.AddMinutes(index),
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, default, 0, 0, 0, 0, 0, 0, 0, TimeSpan.Zero, 0, 0), options)));
        var environment = new Environment(root);
        before = new(environment); after = new(environment);
        var oldRows = await ReadBefore(); var newRows = await ReadAfter();
        if (!oldRows.SequenceEqual(newRows)) throw new InvalidOperationException("History fixtures differ.");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("10-HistoryRead")]
    public ValueTask<IReadOnlyList<SupervisorHealthHistoryPoint>> ReadBefore() => before.ReadAsync(DateTime.MinValue, DateTime.MaxValue, 60, default);
    [Benchmark, BenchmarkCategory("10-HistoryRead")]
    public ValueTask<IReadOnlyList<SupervisorHealthHistoryPoint>> ReadAfter() => after.ReadAsync(DateTime.MinValue, DateTime.MaxValue, 60, default);
    [GlobalCleanup]
    public void Cleanup()
    {
        if (root is null) return;
        var resolved = Path.GetFullPath(root);
        var expectedParent = @"E:\IFM\BenchmarkArtifacts\ApiServerPerformance\20261107";
        var name = Path.GetFileName(resolved);
        if (!string.Equals(Path.GetDirectoryName(resolved), expectedParent, StringComparison.OrdinalIgnoreCase)
            || !name.StartsWith("history-fixture-", StringComparison.Ordinal)
            || !Guid.TryParseExact(name[16..], "N", out _)) throw new InvalidOperationException("Unexpected history fixture path.");
        if (Directory.Exists(resolved))
        {
            if ((File.GetAttributes(resolved) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("History fixture root must not be a link.");
            Directory.Delete(resolved, true);
        }
    }

    sealed class Environment(string path) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Benchmark";
        public string ApplicationName { get; set; } = "Fixture";
        public string ContentRootPath { get; set; } = path;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
