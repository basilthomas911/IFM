using TomasAI.IFM.Application.Api.Server.Core.Http.OutputCaching;
using TomasAI.IFM.Application.Api.Server.Core.Http.Serialization;
using Serilog;
using Serilog.Events;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Api.Server.Core.DependencyInjection;

public static partial class CoreServiceRegistration
{
    public static WebApplicationBuilder ConfigureApiServer(WebApplicationBuilder builder, out Microsoft.Extensions.Logging.ILogger logger)
    {
        var siContainer = new Container();
        siContainer.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        var loggerConfiguration = new LoggerConfiguration()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
        .MinimumLevel.Override(
        "Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService",
        LogEventLevel.Fatal)
        .MinimumLevel.Override("System", LogEventLevel.Error)
        .Enrich.FromLogContext();
        if (builder.Environment.IsDevelopment()
        || builder.Configuration.GetValue("Logging:Console:Enabled", false))
        {
            loggerConfiguration.WriteTo.Console();
        }
        if (builder.Configuration.GetValue("Telemetry:Logs:Enabled", false))
        loggerConfiguration.WriteTo.Sink(new TomasAI.IFM.Framework.Telemetry.Logging.OtlpStructuredLogSink(
        builder.Configuration, "TomasAI.IFM.Application.Api.Server"));
        Log.Logger = loggerConfiguration
        .WriteTo.Async(
        sink => sink.File(
        new Serilog.Formatting.Json.JsonFormatter(renderMessage: true),
        "Logs/ifm-apiserver-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7),
        bufferSize: 4096,
        blockWhenFull: false,
        monitor: new TomasAI.IFM.Framework.Telemetry.Logging.AsyncLogBufferMonitor())
        .CreateLogger();
        _ = builder.WebHost.UseKestrel();
        _ = builder.Host.UseSerilog();

        logger = new Serilog.Extensions.Logging.SerilogLoggerFactory(Log.Logger, dispose: false)
        .CreateLogger("TomasAI.IFM.Application.Api.Server.Program");
        builder.Services.AddSingleton(logger);

        logger.LogInformationEvent("ApiServer","{Component}.{Method} "+"configure web api server...",nameof(CoreServiceRegistration),nameof(ConfigureApiServer));
        builder.Services.ConfigureHttpJsonOptions(options =>
        ApiServerJson.Configure(options.SerializerOptions));
        builder.Services.AddOutputCache(options =>
        {
            options.SizeLimit = 16 * 1024 * 1024;
            options.MaximumBodySize = 2 * 1024 * 1024;
            options.AddPolicy(
            ApiOutputCachePolicies.HealthSnapshot,
            new HealthOutputCachePolicy());
            options.AddPolicy(ApiOutputCachePolicies.OperationalSnapshot, policy =>
            policy.Expire(ApiOutputCachePolicies.OperationalLifetime));
        });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSingleton(siContainer);
        builder.Services.AddSimpleInjector(siContainer);

        return builder;
    }
}
