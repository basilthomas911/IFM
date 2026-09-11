using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Server;

namespace TomasAI.IFM.Domain.Application.Actor.UnitTests;

public sealed class DeploymentIdentityMonitorTests : IDisposable
{
    readonly string _directory = Path.Combine(Path.GetTempPath(), $"ifm-deployment-identity-{Guid.NewGuid():N}");
    readonly DeploymentIdentityOptions _options = new()
    {
        RequiredArtifacts = ["Api.dll", "Trade.dll"]
    };

    public DeploymentIdentityMonitorTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task Matching_process_start_artifacts_and_manifest_are_healthy()
    {
        WriteArtifact("Api.dll", "api-v1");
        WriteArtifact("Trade.dll", "trade-v1");
        WriteManifest("build-v1");
        var monitor = new DeploymentIdentityMonitor(_options, _directory, true);

        var validation = monitor.EnsureStartupValid();
        var health = await new DeploymentIdentityHealthCheck(monitor)
            .CheckHealthAsync(new HealthCheckContext());

        Assert.True(validation.Valid);
        Assert.Equal("build-v1", validation.BuildId);
        Assert.Equal(HealthStatus.Healthy, health.Status);
    }

    [Fact]
    public async Task Rebuilt_artifact_and_manifest_make_the_existing_process_unhealthy()
    {
        WriteArtifact("Api.dll", "api-v1");
        WriteArtifact("Trade.dll", "trade-v1");
        WriteManifest("build-v1");
        var monitor = new DeploymentIdentityMonitor(_options, _directory, true);

        WriteArtifact("Api.dll", "api-v2");
        WriteManifest("build-v2");
        var health = await new DeploymentIdentityHealthCheck(monitor)
            .CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, health.Status);
        Assert.Contains(monitor.Validate().Errors,
            error => error.Contains("Process loaded a stale 'Api.dll'", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_manifest_prevents_standalone_api_startup()
    {
        WriteArtifact("Api.dll", "api-v1");
        WriteArtifact("Trade.dll", "trade-v1");
        var monitor = new DeploymentIdentityMonitor(_options, _directory, true);

        var exception = Assert.Throws<InvalidOperationException>(() => monitor.EnsureStartupValid());

        Assert.Contains("manifest", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void In_process_test_host_does_not_require_a_deployment_manifest()
    {
        var monitor = new DeploymentIdentityMonitor(_options, _directory, false);

        var validation = monitor.EnsureStartupValid();

        Assert.False(validation.Enforced);
        Assert.True(validation.Valid);
    }

    [Fact]
    public void Enforcement_stops_a_process_whose_deployment_changed_after_startup()
    {
        WriteArtifact("Api.dll", "api-v1");
        WriteArtifact("Trade.dll", "trade-v1");
        WriteManifest("build-v1");
        var monitor = new DeploymentIdentityMonitor(_options, _directory, true);
        var lifetime = new RecordingLifetime();
        var service = new DeploymentIdentityEnforcementService(
            monitor, lifetime, NullLogger<DeploymentIdentityEnforcementService>.Instance);

        WriteArtifact("Trade.dll", "trade-v2");
        WriteManifest("build-v2");

        Assert.True(service.EnforceOnce());
        Assert.True(lifetime.StopRequested);
    }

    void WriteArtifact(string fileName, string content) =>
        File.WriteAllText(Path.Combine(_directory, fileName), content);

    void WriteManifest(string buildId)
    {
        var artifacts = _options.RequiredArtifacts.ToDictionary(
            fileName => fileName,
            fileName => Convert.ToHexString(SHA256.HashData(
                File.ReadAllBytes(Path.Combine(_directory, fileName)))));
        File.WriteAllText(
            Path.Combine(_directory, DeploymentIdentityOptions.DefaultManifestFileName),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                buildId,
                generatedAtUtc = DateTime.UtcNow,
                artifacts
            }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    sealed class RecordingLifetime : IHostApplicationLifetime
    {
        public bool StopRequested { get; private set; }
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => StopRequested = true;
    }
}
