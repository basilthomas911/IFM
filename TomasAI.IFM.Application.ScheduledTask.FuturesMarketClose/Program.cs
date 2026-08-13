using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.Application.Shared.ServiceApi;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .CreateLogger();
        builder.Services.AddSerilog();

        builder.Services.AddSingleton<NatsConnectionManager>();
        builder.Services.AddSingleton<IActorProducer>(services => new NatsActorProducer(
            new NatsProducerOptions
            {
                Url = builder.Configuration["Nats:Url"] ?? "nats://localhost:4222"
            },
            NullLogger.Instance,
            services.GetRequiredService<NatsConnectionManager>()));
        builder.Services.AddSingleton<IDatabaseBackupCommandApi, DatabaseBackupCommandApi>();
        builder.Services.AddSingleton<IApplicationCommandApi, ApplicationCommandApi>();
        builder.Services.AddHostedService<Worker>();

        using var host = builder.Build();
        await host.RunAsync().ConfigureAwait(false);
    }
}
