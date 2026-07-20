using System;
using System.IO;
using System.Diagnostics;
using Serilog;
using Serilog.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Command.Client;
using TomasAI.IFM.Application.Query.Client;
using TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.Shared.Application.ServiceApi;

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
        services.AddLogging(configure => configure.AddSerilog());
        services.AddSingleton(loggerFactory.CreateLogger("IFM-ScheduledTask-FuturesMarketClose"));
        services.AddSingleton<IEventConsumerOptions>(sp => new KafkaEventConsumerOptions(null, hostCtx.Configuration.GetValue<string>("AppSettings:EventConsumer:BootstrapServers"), true));
        services.AddSingleton<ISystemAdminUIEventConsumer, SystemAdminUIEventConsumer>();
        services.AddSingleton<IRestApiSerializer, NewtonSoftJsonSerializer>();
        services.AddSingleton<ICommandServiceRestApiOptions>(sp => new CommandServiceRestApiOptions(hostCtx.Configuration.GetValue<string>("AppSettings:CommandServerBaseUri")));
        services.AddSingleton<ICommandService, CommandServiceRestApiClient>();
        services.AddSingleton<ISystemAdminCommandApi, SystemAdminCommandApi>();
        services.AddSingleton<IQueryServiceRestApiOptions>(sp => new QueryServiceRestApiOptions(hostCtx.Configuration.GetValue<string>("AppSettings:QueryServerBaseUri")));
        services.AddSingleton<IQueryService, QueryServiceRestClientApi>();
        services.AddSingleton<ISystemAdminQueryApi, SystemAdminQueryApi>();
        services.AddSingleton<IApplicationCommandApi, ApplicationCommandApi>();
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
