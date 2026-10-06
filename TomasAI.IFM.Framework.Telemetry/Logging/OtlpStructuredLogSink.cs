using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using Serilog.Core;
using Serilog.Events;

namespace TomasAI.IFM.Framework.Telemetry.Logging;

/// <summary>Forwards accepted Serilog events once to the bounded OTLP batch exporter while preserving scalar attributes.</summary>
public sealed class OtlpStructuredLogSink : ILogEventSink, IDisposable
{
    readonly ILoggerFactory factory;
    readonly ConcurrentDictionary<string, ILogger> loggers = new(StringComparer.Ordinal);

    public OtlpStructuredLogSink(IConfiguration configuration, string serviceName)
    {
        var section = configuration.GetSection("Telemetry:Logs");
        if (!Uri.TryCreate(section["OtlpEndpoint"], UriKind.Absolute, out var endpoint))
            throw new InvalidOperationException("Telemetry:Logs:OtlpEndpoint must be an absolute URI.");
        var protocol = section["OtlpProtocol"] ?? "http/protobuf";
        if (protocol is not ("http/protobuf" or "grpc"))
            throw new InvalidOperationException("Telemetry:Logs:OtlpProtocol must be http/protobuf or grpc.");
        factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.ParseStateValues = true;
            options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(section["ServiceName"] ?? serviceName));
            options.AddOtlpExporter(exporter =>
            {
                exporter.Endpoint = endpoint;
                exporter.Protocol = protocol == "http/protobuf" ? OtlpExportProtocol.HttpProtobuf : OtlpExportProtocol.Grpc;
                exporter.Headers = section["Headers"];
                exporter.ExportProcessorType = ExportProcessorType.Batch;
                exporter.BatchExportProcessorOptions.MaxQueueSize = 4096;
                exporter.BatchExportProcessorOptions.MaxExportBatchSize = 256;
                exporter.BatchExportProcessorOptions.ScheduledDelayMilliseconds = 1000;
                exporter.BatchExportProcessorOptions.ExporterTimeoutMilliseconds = 5000;
            });
        }));
    }

    public void Emit(LogEvent logEvent)
    {
        var category = logEvent.Properties.TryGetValue("SourceContext", out var source) && source is ScalarValue { Value: string name }
            ? name : "IFM";
        var logger = loggers.GetOrAdd(category, factory.CreateLogger);
        var level = (LogLevel)(int)logEvent.Level;
        var state = new List<KeyValuePair<string, object?>>(logEvent.Properties.Count + 1);
        foreach (var property in logEvent.Properties)
            state.Add(new(property.Key, property.Value is ScalarValue scalar ? Normalize(scalar.Value) : property.Value.ToString()));
        if (!logEvent.Properties.ContainsKey("Component")) state.Add(new("Component", category));
        if (logEvent.TraceId is { } traceId) state.Add(new("TraceId", traceId.ToString()));
        if (logEvent.SpanId is { } spanId) state.Add(new("SpanId", spanId.ToString()));
        state.Add(new("{OriginalFormat}", logEvent.MessageTemplate.Text));
        logger.Log(level, default, state, logEvent.Exception, (_, _) => logEvent.RenderMessage());
    }

    static object? Normalize(object? value) => value switch
    {
        null or string or bool or long or double or int or float => value,
        byte or sbyte or short or ushort or uint => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture),
        ulong number when number <= long.MaxValue => (long)number,
        decimal number => (double)number,
        DateOnly date => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        DateTime date => date.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        IFormattable scalar => scalar.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
    public void Dispose() => factory.Dispose();
}
