using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using TomasAI.IFM.Framework.Telemetry.Logging;
using TomasAI.IFM.Framework.Telemetry.Metrics;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.Application.Shared.ServiceApi;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Application.ScheduledTask.Shared;

namespace TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        var logging = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration);
        if (builder.Configuration.GetValue<bool>("Telemetry:Logs:Enabled"))
            logging.WriteTo.Sink(new OtlpStructuredLogSink(builder.Configuration, "IFM-ScheduledTask-FuturesMarketClose"));
        Log.Logger = logging.CreateLogger();
        builder.Services.AddIfmMetrics(builder.Configuration, "IFM-ScheduledTask-FuturesMarketClose");
        builder.Services.AddSerilog();

        var maintenanceOptions = new NatsJetStreamEndOfDayMaintenanceOptions();
        builder.Configuration.GetSection("JetStreamEndOfDayPurge").Bind(maintenanceOptions);
        maintenanceOptions.Url = builder.Configuration["Nats:Url"] ?? maintenanceOptions.Url;
        maintenanceOptions.Validate();

        builder.Services.AddSingleton(maintenanceOptions);
        builder.Services.AddSingleton<NatsConnectionManager>();
        builder.Services.AddSingleton<INatsJetStreamEndOfDayMaintenance, NatsJetStreamEndOfDayMaintenance>();
        builder.Services.AddSingleton<IActorProducer>(services => new NatsActorProducer(
            new NatsProducerOptions
            {
                Url = builder.Configuration["Nats:Url"] ?? "nats://localhost:4222"
            },
            NullLogger.Instance,
            services.GetRequiredService<NatsConnectionManager>()));
        builder.Services.AddSingleton<IDatabaseBackupCommandApi, DatabaseBackupCommandApi>();
        builder.Services.AddSingleton<IApplicationCommandApi, ApplicationCommandApi>();
        builder.Services.AddSingleton<TomasAI.IFM.Domain.MarketData.Shared.ServiceApi.IMarketDataQueryApi, MarketDataQueryApi>();
        builder.Services.AddSingleton<TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi.IMarketDataFeedCommandApi, MarketDataFeedCommandApi>();
        builder.Services.AddSingleton<TomasAI.IFM.Domain.Trade.Shared.ServiceApi.IStrategyPositionCommandApi, StrategyPositionCommandApi>();
        builder.Services.AddSingleton<TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi.IScheduledTaskQueryApi, ScheduledTaskQueryApi>();
        builder.Services.AddSingleton<TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi.IScheduledTaskCommandApi, ScheduledTaskCommandApi>();
        builder.Services.AddSingleton(new NatsEventListenerOptions { Url = builder.Configuration["Nats:Url"] ?? "nats://localhost:4222" });
        builder.Services.AddSingleton<ScheduledTaskEventCompletion>();
        builder.Services.AddSingleton<ScheduledTaskBusinessReceipts>();
        builder.Services.AddScheduledTaskRuntime();
        builder.Services.AddHostedService<Worker>();

        using var host = builder.Build();
        var outcome = host.Services.GetRequiredService<ScheduledTaskOutcome>();
        await host.RunAsync().ConfigureAwait(false);
        return outcome.ExitCode;
    }
}
