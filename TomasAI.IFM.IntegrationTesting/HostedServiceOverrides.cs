using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TomasAI.IFM.IntegrationTesting;

/// <summary>Suppresses one hosted lifecycle without relying on DI factory delegate return types.</summary>
public static class HostedServiceOverrides
{
    public static void Suppress<TService>(IServiceCollection services) where TService : class, IHostedService
    {
        foreach (var descriptor in services.Where(item => item.ServiceType == typeof(IHostedService)).ToArray())
        {
            if (descriptor.ImplementationType is { } implementation && typeof(TService).IsAssignableFrom(implementation))
                services.Remove(descriptor);
            else if (descriptor.ImplementationFactory is { } create)
            {
                services.Remove(descriptor);
                services.Add(ServiceDescriptor.Describe(typeof(IHostedService), provider =>
                {
                    var service = (IHostedService)create(provider);
                    return service is TService ? new PassiveHostedService() : service;
                }, descriptor.Lifetime));
            }
        }
    }

    sealed class PassiveHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
