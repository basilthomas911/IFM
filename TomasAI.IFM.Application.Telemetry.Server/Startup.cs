using Serilog;
using Serilog.Events;
using TomasAI.IFM.Application.Command.Client;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Telemetry.ServiceApi;
using System.Net;

namespace TomasAI.IFM.Application.Telemetry.Server;

public static class StartUp
{
    public static WebApplicationBuilder ConfigureWebApp(this WebApplicationBuilder builder, out Microsoft.Extensions.Logging.ILogger logger ) 
    {
        builder.WebHost
            .ConfigureAppConfiguration((ctx, config) => {
                config.SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                    .MinimumLevel.Override("System", LogEventLevel.Error)
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .WriteTo.File("logs\\ifm-telemetry-server.log")
                    .CreateLogger();
            })
            .UseKestrel()
            .UseSerilog();

        // configure web app...
        var serviceProvider = builder.Services.BuildServiceProvider();
        logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        builder.Services.AddSingleton(logger);
        logger.LogInformationEvent("TelemetryServer", "configure web app...");
        builder.Services.AddHttpClient("HttpRestApi").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
        {
            UseDefaultCredentials = true,
            Credentials = new NetworkCredential("", ""),
        });
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        return builder;
    }

    public static IServiceCollection RegisterServices(this IServiceCollection services, ConfigurationManager config, Microsoft.Extensions.Logging.ILogger logger) 
    {
        // add web app services...
        logger.LogInformationEvent("TelemetryServer", "add web app services...");
        services.AddSingleton<IJsonSerializer, NewtonSoftJsonSerializer>();
        services.AddSingleton<ICommandServiceRestApiOptions>(_ => new CommandServiceRestApiOptions(config.GetValue<string>("AppSettings:CommandServerBaseUri")!));
        services.AddSingleton<ICommandService, CommandServiceRestApiClient>();
        services.AddSingleton<ITelemetryCommandApi, TelemetryCommandApi>();
        return services;
    }

    public static WebApplication ConfigureRequestPipeline(this WebApplication app, Microsoft.Extensions.Logging.ILogger logger) 
    {
        // configure the HTTP request pipeline...
        logger.LogInformationEvent("TelemetryServer", "configure HTTP request pipeline...");
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
                options.RoutePrefix = string.Empty;
            });
        }
        else
        {
            app.UseHttpsRedirection();
        }
        app.UseAuthorization();
        app.MapControllers();
        logger.LogInformationEvent("TelemetryServer", "web app configuration completed");
        return app;
    }
}
