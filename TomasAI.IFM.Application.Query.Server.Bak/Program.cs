using System;
using System.IO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using TomasAI.IFM.Framework.Telemetry.Logging.Serilog;

namespace TomasAI.IFM.Application.Query.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                CreateWebHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "IFM QueryServer: start-up failed");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
             => WebHost.CreateDefaultBuilder(args)
                 .ConfigureAppConfiguration(ConfigConfiguration)
                 .UseKestrel()
                 .UseSerilog()
                 .UseStartup<Startup>();

        static void ConfigConfiguration(WebHostBuilderContext ctx, IConfigurationBuilder configBuilder)
        {
            configBuilder.SetBasePath(Directory.GetCurrentDirectory())
                 .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                 .AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);

            var config = configBuilder.Build();
            var telemetryServerBaseUri = config.GetValue<string>("AppSettings:TelemetryServerBaseUri");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                .MinimumLevel.Override("System", LogEventLevel.Error)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.Http(requestUri: telemetryServerBaseUri, httpClient: new SerilogHttpClient())
                .CreateLogger();
        }
        
    }
}
