using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.ScheduledTask.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.ScheduledTask.SetClosingPrice;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger();
        builder.Services.AddSerilog();
        builder.Services.AddSingleton<NatsConnectionManager>();
        builder.Services.AddSingleton<IActorProducer>(services => new NatsActorProducer(
            new NatsProducerOptions { Url = builder.Configuration["Nats:Url"] ?? "nats://localhost:4222" },
            NullLogger.Instance,
            services.GetRequiredService<NatsConnectionManager>()));
        builder.Services.AddSingleton<IMarketDataQueryApi, MarketDataQueryApi>();
        builder.Services.AddSingleton<IMarketDataFeedQueryApi, MarketDataFeedQueryApi>();
        builder.Services.AddSingleton<IMarketDataFeedCommandApi, MarketDataFeedCommandApi>();
        builder.Services.AddSingleton<ITradePlacementCommandApi, TradePlacementCommandApi>();
        builder.Services.AddScheduledTaskRuntime();
        builder.Services.AddHostedService<Worker>();

        using var host = builder.Build();
        var outcome = host.Services.GetRequiredService<ScheduledTaskOutcome>();
        await host.RunAsync().ConfigureAwait(false);
        return outcome.ExitCode;
    }
}
