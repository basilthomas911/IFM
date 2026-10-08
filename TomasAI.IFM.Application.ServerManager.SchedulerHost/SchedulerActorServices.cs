using Microsoft.Extensions.Configuration;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;
/// <summary>Registers actor transport for the existing scheduler runtime.</summary>
public static class SchedulerActorServices
{
    /// <summary>Adds a host-specific durable control listener and typed request APIs.</summary>
    public static IServiceCollection AddSchedulerActorServices(this IServiceCollection services, IConfiguration configuration)
    {
        var producer = configuration.GetSection("Nats:Producer").Get<NatsProducerOptions>() ?? new();
        var listener = configuration.GetSection(NatsJetStreamEventListenerOptions.SectionName).Get<NatsJetStreamEventListenerOptions>() ?? new();
        listener.MaxDeliver = 3;
        listener.Validate();
        services.AddSingleton<INatsProducerOptions>(producer);
        services.AddSingleton<INatsJetStreamEventListenerOptions>(listener);
        services.AddSingleton<NatsConnectionManager>();
        services.AddSingleton<IActorProducer>(sp => new NatsActorProducer(producer, sp.GetRequiredService<ILogger<NatsActorProducer>>(), sp.GetRequiredService<NatsConnectionManager>()));
        services.AddSingleton<IJSActorEventListener>(sp => new NatsJetStreamEventListener(listener, sp.GetRequiredService<ILogger<NatsJetStreamEventListener>>(), sp.GetRequiredService<NatsConnectionManager>()));
        services.AddSingleton<TomasAI.IFM.Domain.Reference.Shared.ServiceApi.IReferenceCommandApi, ReferenceCommandApi>();
        services.AddSingleton<TomasAI.IFM.Domain.Reference.Shared.ServiceApi.IReferenceQueryApi, ReferenceQueryApi>();
        services.AddSingleton<IScheduledTaskCommandApi, ScheduledTaskCommandApi>();
        services.AddSingleton<IScheduledTaskQueryApi, ScheduledTaskQueryApi>();
        services.AddSingleton<ActorScheduledTaskRecovery>();
        services.AddSingleton<ActorScheduleRuntime>();
        services.AddSingleton<ActorScheduledTaskRunCoordinator>();
        services.AddHostedService<ActorSchedulerHostService>();
        return services;
    }
}
