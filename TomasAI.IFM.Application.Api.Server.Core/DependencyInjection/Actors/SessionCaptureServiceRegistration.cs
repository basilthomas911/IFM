using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.DependencyInjection;

internal static class SessionCaptureServiceRegistration
{
    internal static void Register(IServiceCollection services, ConfigurationManager config, IHostEnvironment? environment,
        Container container, RealtimeMailboxOptions mailbox)
    {
        var options = config.GetSection("Actors:SessionCapture").Get<RealtimeSessionArchiveOptions>() ?? new();
        if (options.Enabled && environment?.IsDevelopment() != true)
            throw new InvalidOperationException("Session source recording is restricted to explicit Development qualification.");
        if (options.Enabled && mailbox.Enabled)
            throw new InvalidOperationException("Capture-only session recording must not activate or share the durable realtime backend.");
        if (options.Enabled && string.Equals(Path.GetFullPath(options.Directory).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(mailbox.Directory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Session source archives require a separate directory from realtime mailbox ownership.");
        services.AddSingleton<Func<IActorSupervisor>>(_ => () => container.GetInstance<IActorSupervisor>());
        services.AddSingleton(options);
        services.AddSingleton<DurableRealtimeSessionArchive>();
        services.AddSingleton<RealtimeSessionRootFence>();
        services.AddSingleton<IRealtimeSessionRecorder>(provider => provider.GetRequiredService<DurableRealtimeSessionArchive>());
        services.AddHealthChecks().AddCheck<HostedServices.SessionCaptureArchiveHealthCheck>("session_capture_archive", tags: ["ready"]);
        services.AddTransient<IJSActorProducer>(provider => new NatsJetStreamActorProducer(provider.GetRequiredService<INatsJetStreamProducerOptions>(),
            provider.GetRequiredService<ILogger<NatsJetStreamActorProducer>>(), provider.GetRequiredService<NatsConnectionManager>(),
            provider.GetService<TimeProvider>(), provider.GetRequiredService<IRealtimeSessionRecorder>(), () => container.GetInstance<IActorSupervisor>()));
        services.AddTransient<IActorProducer>(provider => new NatsActorProducer(provider.GetRequiredService<INatsProducerOptions>(),
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<NatsActorProducer>(), provider.GetRequiredService<NatsConnectionManager>(),
            provider.GetService<TimeProvider>(), provider.GetRequiredService<IRealtimePublicationCapture>(),
            provider.GetRequiredService<IRealtimeSessionRecorder>(), () => container.GetInstance<IActorSupervisor>(),
            provider.GetRequiredService<RealtimeSessionRootFence>()));
    }
}
