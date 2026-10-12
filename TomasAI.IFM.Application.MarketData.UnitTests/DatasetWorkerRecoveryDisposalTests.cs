using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatasetWorkerRecoveryDisposalTests
{
    [Fact]
    public async Task Aliases_and_concurrent_disposal_share_one_cleanup_without_starting_workers()
    {
        var services = new ServiceCollection();
        services.AddSingleton<DatasetWorkerProcessRecoveryService>(_ => new(new(), new()));
        services.AddSingleton<IDatabentoDatasetProcessRecovery>(provider => provider.GetRequiredService<DatasetWorkerProcessRecoveryService>());
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Pricing.ICompositionMarketDataApi>(provider => provider.GetRequiredService<DatasetWorkerProcessRecoveryService>());
        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<DatasetWorkerProcessRecoveryService>();
        Assert.Same(service, provider.GetRequiredService<IDatabentoDatasetProcessRecovery>());
        Assert.Same(service, provider.GetRequiredService<TomasAI.IFM.Application.MarketData.Pricing.ICompositionMarketDataApi>());
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => service.DisposeAsync().AsTask()));
        await provider.DisposeAsync();
        Assert.Empty(service.Current);
    }
}
