using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Registers Core NATS and JetStream event listeners as independent services.
/// </summary>
public static class NatsMessagingServiceCollectionExtensions
{
    public static IServiceCollection AddNatsActorEventListeners(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<INatsEventListenerOptions, NatsEventListenerOptions>();
        services.TryAddSingleton<INatsJetStreamEventListenerOptions, NatsJetStreamEventListenerOptions>();
        services.TryAddSingleton<NatsConnectionManager>();
        services.TryAddSingleton<IActorEventListener>(provider => new NatsActorEventListener(
            provider.GetRequiredService<INatsEventListenerOptions>(),
            CreateLogger<NatsActorEventListener>(provider),
            provider.GetRequiredService<NatsConnectionManager>()));
        services.TryAddSingleton<IJSActorEventListener>(provider => new NatsJetStreamEventListener(
            provider.GetRequiredService<INatsJetStreamEventListenerOptions>(),
            CreateLogger<NatsJetStreamEventListener>(provider),
            provider.GetRequiredService<NatsConnectionManager>()));
        return services;
    }

    static ILogger CreateLogger<T>(IServiceProvider provider) =>
        (ILogger?)provider.GetService<ILoggerFactory>()?.CreateLogger<T>() ?? NullLogger.Instance;
}
