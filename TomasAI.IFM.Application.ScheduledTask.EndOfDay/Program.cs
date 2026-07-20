using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestSharp.Serialization;
using Serilog;
using Serilog.Extensions.Logging;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Shared.Fund.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Application.Command.Client;
using TomasAI.IFM.Application.Query.Client;
using TomasAI.IFM.Application.ScheduledTask.MarketClose;
using TomasAI.IFM.UI.EventConsumer;

try
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((hostCtx, config)
           => config.SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
               .AddJsonFile($"appsettings.{hostCtx.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true))
        .ConfigureServices((hostCtx, services) =>
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(hostCtx.Configuration)
                .CreateLogger();
            var loggerFactory = new SerilogLoggerFactory(Log.Logger);
            services.AddSingleton(loggerFactory.CreateLogger("IFM-ScheduledTask-EndOfDay"));
            services.AddSingleton<IRestSerializer, NewtonSoftJsonSerializer>();
            services.AddSingleton<ICommandServiceRestApiOptions>(sp => new CommandServiceRestApiOptions(hostCtx.Configuration.GetValue<string>("AppSettings:CommandServerBaseUri")));
            services.AddSingleton<ICommandService, CommandServiceHttpClientApi>();
            services.AddSingleton<ITradeCommandApi, TradeCommandApi>();
            services.AddSingleton<IQueryServiceRestApiOptions>(sp => new QueryServiceRestApiOptions(hostCtx.Configuration.GetValue<string>("AppSettings:QueryServerBaseUri")));
            services.AddSingleton<IQueryService, QueryServiceRestApi>();
            services.AddSingleton<IFundQueryApi, FundQueryApi>();
            services.AddSingleton<IMarketDataFeedQueryApi, MarketDataFeedQueryApi>();
            services.AddSingleton<IMarketDataQueryApi, MarketDataQueryApi>();
            services.AddSingleton<ISystemAdminUIEventConsumer, SystemAdminUIEventConsumer>();
            services.AddSingleton<ISystemAdminQueryApi, SystemAdminQueryApi>();
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