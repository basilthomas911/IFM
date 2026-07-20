using Microsoft.Extensions.Hosting;

namespace TomasAI.IFM.Service.TradePlacement.HostedService;

/// <summary>
/// A hosted service that manages the lifecycle of the trade placement event consumer.
/// </summary>
/// <remarks>This service starts and stops the <see cref="ITradePlacementEventConsumer"/> as part of the
/// application's hosted service infrastructure. It ensures that the consumer is properly initialized and disposed
/// during the application's startup and shutdown processes.</remarks>
/// <param name="tradePlacementEventConsumer"></param>
public class TradePlacementHostedService(ITradePlacementEventConsumer tradePlacementEventConsumer) 
    : IHostedService
{
    public async Task StartAsync(CancellationToken stoppingToken) 
        => await tradePlacementEventConsumer.StartAsync();

    public async Task StopAsync(CancellationToken cancellationToken) 
        => await tradePlacementEventConsumer.StopAsync();
}
