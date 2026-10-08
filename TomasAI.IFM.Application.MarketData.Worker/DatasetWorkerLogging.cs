using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using TomasAI.IFM.Framework.Telemetry.Logging;

namespace TomasAI.IFM.Application.MarketData.Worker;

/// <summary>Configures bounded worker file and collector logging without logging credentials.</summary>
internal static class DatasetWorkerLogging
{
    /// <summary>Creates daily JSON files and an OTLP exporter configurable through Telemetry__Logs environment variables.</summary>
    internal static Serilog.ILogger CreateLogger()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Telemetry:Logs:OtlpEndpoint"] = "http://localhost:4318/v1/logs",
            ["Telemetry:Logs:OtlpProtocol"] = "http/protobuf"
        }).AddEnvironmentVariables().Build();
        var logger = new LoggerConfiguration().MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext().Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .Enrich.WithProperty("Service", "TomasAI.IFM.Application.MarketData.Worker")
            .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose)
            .WriteTo.Async(sink => sink.File(new JsonFormatter(renderMessage: true),
                Path.Combine(AppContext.BaseDirectory, "Logs", "ifm-dataset-worker-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7, shared: true),
                bufferSize: 4096, blockWhenFull: false, monitor: new AsyncLogBufferMonitor());
        if (!string.Equals(configuration["Telemetry:Logs:Enabled"], "false", StringComparison.OrdinalIgnoreCase))
            logger.WriteTo.Sink(new OtlpStructuredLogSink(configuration, "TomasAI.IFM.Application.MarketData.Worker"));
        return logger.CreateLogger();
    }

    /// <summary>Removes inherited authentication secrets from startup failure text.</summary>
    internal static string Redact(string detail)
    {
        foreach (var name in new[] { "DATABENTO_API_KEY", "IFM_DATASET_WORKER_BOOTSTRAP" })
            if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } secret)
                detail = detail.Replace(secret, "[redacted]", StringComparison.Ordinal);
        return detail;
    }
}
