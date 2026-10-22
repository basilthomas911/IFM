using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Host;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
namespace TomasAI.IFM.Application.MarketData.UnitTests;
public sealed class MarketDataHostCompositionTests
{
    [Theory]
    [InlineData("Development", true, true, 2000)]
    [InlineData("Development", true, false, 0)]
    [InlineData("Development", false, true, 0)]
    [InlineData("Production", true, true, 0)]
    [InlineData("PaperTrading", true, true, 0)]
    public void Host_clock_tolerance_requires_explicit_development_live_qualification(
        string profile, bool stage3, bool optIn, int expectedMilliseconds)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MarketDataHost:DeploymentProfile"] = profile,
            ["MarketDataHost:DataSource"] = "DatabentoLive",
            ["MarketDataRecovery:Stage3:Enabled"] = stage3.ToString(),
            ["MarketDataRecovery:Stage3:AllowDevelopmentLiveQualification"] = optIn.ToString()
        }).Build();
        var services = new ServiceCollection();
        services.AddMarketDataBackend(config);
        var policy = services.Single(d => d.ServiceType == typeof(TomasAI.IFM.Application.MarketData.Pricing.PricingSourceClockPolicy))
            .ImplementationInstance as TomasAI.IFM.Application.MarketData.Pricing.PricingSourceClockPolicy;
        Assert.Equal(expectedMilliseconds, policy!.MaximumLeadMilliseconds);
    }

    [Fact]
    public async Task Full_Host_backend_resolves_without_API_actor_runtime_or_provider_epoch()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "TomasAI.IFM.sln"))) root = root.Parent;
        var config = new ConfigurationBuilder().SetBasePath(Path.Combine(root!.FullName, "TomasAI.IFM.Application.Api.Server"))
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json").AddEnvironmentVariables().Build();
        var services = new ServiceCollection();
        services.AddLogging(); services.AddHttpClient(); services.AddSingleton<IConfiguration>(config);
        var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns("Development"); services.AddSingleton(environment);
        services.AddSingleton<NatsConnectionManager>();
        services.AddSingleton(p => new MarketDataHostConnection(p.GetRequiredService<NatsConnectionManager>(), "nats://127.0.0.1:4222", "compositiontest"));
        services.AddMarketDataBackend(config);
        services.AddSingleton<MarketDataWorkerHost>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Assert.IsType<MarketDataApiBackend>(provider.GetRequiredService<IMarketDataApi>());
        Assert.Equal(2000, provider.GetRequiredService<TomasAI.IFM.Application.MarketData.Pricing.PricingSourceClockPolicy>()
            .MaximumLeadMilliseconds);
        Assert.NotNull(provider.GetRequiredService<TomasAI.IFM.Application.MarketData.Host.QualifiedIndividualOptionFeeds>());
        Assert.NotNull(provider.GetRequiredService<TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainBackgroundUpdater>());
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName?.Contains("IActorSupervisor", StringComparison.Ordinal) == true);
    }
}
