using Serilog;
using Serilog.Formatting.Json;
using TomasAI.IFM.Framework.Telemetry.Logging;

namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Persists Host diagnostics independently of Server Manager's short rolling console history.</summary>
internal static class NativeHostLogging
{
    internal static Serilog.ILogger Create(IConfiguration configuration)
    {
        var logger = new LoggerConfiguration().MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext().Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .Enrich.WithProperty("Service", "TomasAI.IFM.Application.MarketData.Host")
            .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .WriteTo.Async(sink => sink.File(new JsonFormatter(renderMessage: true, formatProvider: System.Globalization.CultureInfo.InvariantCulture),
                Path.Combine(AppContext.BaseDirectory, "Logs", "ifm-marketdata-host-.log"),
                rollingInterval: RollingInterval.Day, fileSizeLimitBytes: 128 * 1024 * 1024,
                rollOnFileSizeLimit: true, retainedFileCountLimit: 14),
                bufferSize: 4096, blockWhenFull: false, monitor: new AsyncLogBufferMonitor());
        if (configuration.GetValue("Telemetry:Logs:Enabled", true))
            logger.WriteTo.Sink(new OtlpStructuredLogSink(configuration, "TomasAI.IFM.Application.MarketData.Host"));
        return logger.CreateLogger();
    }
}
