using TomasAI.IFM.Application.Api.Server.Core.Deployment.Identity;
using System.Security.Cryptography;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class DeploymentIdentityHealthBenchmarks
{
    string directory = null!;
    DeploymentIdentityMonitor monitor = null!;

    [GlobalSetup]
    public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), $"ifm-deployment-benchmark-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var options = new DeploymentIdentityOptions { RequiredArtifacts = ["Api.dll", "Trade.dll"] };
        foreach (var artifact in options.RequiredArtifacts)
        {
            var bytes = new byte[1024 * 1024];
            Random.Shared.NextBytes(bytes);
            File.WriteAllBytes(Path.Combine(directory, artifact), bytes);
        }
        var artifacts = options.RequiredArtifacts.ToDictionary(
            fileName => fileName,
            fileName => Convert.ToHexString(SHA256.HashData(
                File.ReadAllBytes(Path.Combine(directory, fileName)))));
        File.WriteAllText(
            Path.Combine(directory, DeploymentIdentityOptions.DefaultManifestFileName),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                buildId = "benchmark",
                generatedAtUtc = DateTime.UtcNow,
                artifacts
            }));
        monitor = new DeploymentIdentityMonitor(options, directory, true);
        monitor.EnsureStartupValid();
    }

    [Benchmark(Baseline = true)]
    public DeploymentIdentityValidation RehashDeploymentArtifacts() => monitor.Validate();

    [Benchmark]
    public DeploymentIdentityValidation ReadPublishedValidation() => monitor.Current;

    [GlobalCleanup]
    public void Cleanup() => Directory.Delete(directory, recursive: true);
}
