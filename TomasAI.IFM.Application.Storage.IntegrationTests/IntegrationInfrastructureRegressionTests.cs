using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TomasAI.IFM.IntegrationTesting;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests;

public sealed class IntegrationInfrastructureRegressionTests
{
    [Fact]
    public async Task Suppression_handles_wrapped_factories_and_preserves_other_hosted_services()
    {
        IServiceCollection services = new ServiceCollection();
        var suppressed = new SuppressedService();
        var retained = new RetainedService();
        services.AddSingleton<IHostedService>(_ => suppressed);
        services.AddSingleton<IHostedService>(_ => retained);
        // Erase the factory return type in the same way host customization does.
        foreach (var descriptor in services.ToArray())
        {
            var create = descriptor.ImplementationFactory!;
            services.Remove(descriptor);
            services.Add(ServiceDescriptor.Singleton(typeof(IHostedService), provider => create(provider)));
        }
        HostedServiceOverrides.Suppress<SuppressedService>(services);
        HostedServiceOverrides.Suppress<SuppressedService>(services);
        await using var provider = services.BuildServiceProvider();
        foreach (var service in provider.GetServices<IHostedService>())
        {
            await service.StartAsync(default);
            await service.StopAsync(default);
        }
        Assert.Equal(0, suppressed.Starts);
        Assert.Equal(1, retained.Starts);
        Assert.Equal(1, retained.Stops);
    }

    [Fact]
    public void Child_manifest_hashes_actual_artifacts_and_cleans_up_its_owned_file()
    {
        var names = new[] { "TomasAI.IFM.Application.Api.Server.dll", "TomasAI.IFM.Application.Api.Server.Core.dll", "TomasAI.IFM.Domain.Trade.dll" };
        var directory = Path.Combine(Path.GetTempPath(), "ifm-manifest-regression-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string? manifestPath = null;
        try
        {
            foreach (var name in names) File.WriteAllText(Path.Combine(directory, name), name);
            using (var manifest = new ApiProcessDeploymentManifest(directory))
            {
                var process = new ProcessStartInfo();
                manifest.Configure(process);
                manifestPath = process.Environment["DeploymentIdentity__ManifestPath"]!;
                using var json = JsonDocument.Parse(File.ReadAllText(manifestPath));
                Assert.Equal(1, json.RootElement.GetProperty("SchemaVersion").GetInt32());
                foreach (var name in names)
                    Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, name)))),
                        json.RootElement.GetProperty("Artifacts").GetProperty(name).GetString());
            }
            Assert.False(File.Exists(manifestPath));
            Assert.False(Directory.Exists(Path.GetDirectoryName(manifestPath)));
        }
        finally
        {
            foreach (var name in names) File.Delete(Path.Combine(directory, name));
            Directory.Delete(directory);
        }
    }

    sealed class SuppressedService : RetainedService;

    class RetainedService : IHostedService
    {
        public int Starts { get; private set; }
        public int Stops { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken) { Starts++; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken) { Stops++; return Task.CompletedTask; }
    }
}
