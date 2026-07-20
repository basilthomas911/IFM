using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using SimpleInjector;
using Serilog.Events;
using TomasAI.IFM.Framework.Telemetry.Logging.Serilog;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

namespace TomasAI.IFM.Application.Event.Server;

public class Program
{
    static readonly Container _container = new ();

    public static void Main(string[] args)
    {
        try
        {
            var host = CreateHostBuilder(args)
                .Build()
                .UseSimpleInjector(_container);
            _container.Verify();
            host.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "IFM EventServer: start-up failed");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args)
        => Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureAppConfiguration(ConfigConfiguration)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddLogging(configure => configure.AddSerilog());
                services.AddHttpClient("HttpRestApi").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
                {
                    UseDefaultCredentials = true,
                    Credentials = new NetworkCredential("", ""),
                });
                services.ConfigureEventServices(hostContext.Configuration, _container);
            });

    static void ConfigConfiguration(HostBuilderContext ctx, IConfigurationBuilder configBuilder)
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
            .WriteTo.Http(requestUri: telemetryServerBaseUri, httpClient: new SerilogHttpClient(), queueLimitBytes: 10000)
            .CreateLogger();
    }

}

public class AuthenticationHttpMessageHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Get the token or other type of credentials here
        // string scheme = ... // E.g. "Bearer", "Basic" etc.
        // string credentials = ... // E.g. formatted token, user/password etc.

        //request.Headers.Authorization =  new AuthenticationHeaderValue(scheme, credentials);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
