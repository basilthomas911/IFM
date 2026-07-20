using System.Diagnostics;
using Serilog;
using Serilog.Extensions.Logging;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.Application.ServiceApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Application.Command.Client;
using TomasAI.IFM.Application.Query.Client;
using ScheduledTask.TrainFuturesItiPredictiveModel;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ServiceApi;
using TomasAI.IFM.Application.Command.Client.PredictiveModel.FuturesItiTrend;

try
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((hostCtx, config)
            => config.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{hostCtx.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true))
        .ConfigureServices((hostCtx, services) =>
        {
            services.AddLogging(configure => configure.AddSerilog());
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(hostCtx.Configuration)
                .CreateLogger();
            var loggerFactory = new SerilogLoggerFactory(Log.Logger);
            services.AddSingleton(loggerFactory.CreateLogger("IFM-ScheduledTask-TrainFuturesItiPredictiveModel"));
            services.AddSingleton<IRestApiSerializer, NewtonSoftJsonSerializer>();
            services.AddSingleton<ICommandServiceRestApiOptions>(sp => new CommandServiceRestApiOptions(hostCtx.Configuration.GetValue<string>("AppSettings:CommandServerBaseUri")));
            services.AddSingleton<ICommandService, CommandServiceRestApiClient>();
            services.AddSingleton<IMarketDataQueryApi, MarketDataQueryApi>();
            services.AddSingleton<IQueryServiceRestApiOptions>(sp => new QueryServiceRestApiOptions(hostCtx.Configuration.GetValue<string>("AppSettings:QueryServerBaseUri")));
            services.AddSingleton<IQueryService, QueryServiceRestClientApi>();
            services.AddSingleton<IMarketDataQueryApi, MarketDataQueryApi>();
            services.AddSingleton<IFuturesItiTrendCommandApi, FuturesItiTrendCommandApi>();
            services.AddHostedService<Worker>();
        })
        .Build();
    await host.RunAsync();
}
catch { }
finally
{
    Process.GetCurrentProcess().Kill();
}
