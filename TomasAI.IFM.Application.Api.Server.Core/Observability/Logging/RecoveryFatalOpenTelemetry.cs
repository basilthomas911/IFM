using TomasAI.IFM.Application.Api.Server.Core.Recovery.Shutdown;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;

namespace TomasAI.IFM.Application.Api.Server.Core.Observability.Logging;

/// <summary>Independent fatal-only OTLP logger; ordinary API logging remains unchanged.</summary>
public sealed class RecoveryFatalOpenTelemetry : IRecoveryFatalTelemetry, IDisposable
{
    readonly ILoggerFactory factory;
    readonly ILogger logger;

    public RecoveryFatalOpenTelemetry(IConfiguration configuration)
    {
        var endpointText = configuration["Telemetry:FatalRecovery:OtlpEndpoint"]
            ?? configuration["Telemetry:Metrics:OtlpEndpoint"];
        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint))
            throw new InvalidOperationException("Fatal recovery OTLP endpoint must be an absolute URI.");
        var protocol = configuration["Telemetry:FatalRecovery:OtlpProtocol"]
            ?? configuration["Telemetry:Metrics:OtlpProtocol"];
        factory = LoggerFactory.Create(builder => builder.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.AddOtlpExporter(exporter =>
            {
                exporter.Endpoint = endpoint;
                exporter.Protocol = string.Equals(protocol, "http/protobuf",
                    StringComparison.OrdinalIgnoreCase)
                    ? OtlpExportProtocol.HttpProtobuf : OtlpExportProtocol.Grpc;
            });
        }));
        logger = factory.CreateLogger("IFM.FatalRecovery");
    }

    public Task EmitAsync(FatalRecoveryReport report, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogCritical(
            "{Component}.{Method} "+"Unrecoverable Databento recovery: {Reason}; CorrelationId={CorrelationId}; ValueDate={ValueDate}; Attempts={Attempts}; FailedStage={FailedStage}; Detail={Detail}",nameof(RecoveryFatalOpenTelemetry),nameof(EmitAsync),            report.Reason,report.Request.CorrelationId,report.Request.ValueDate,            report.Result.Attempts,report.Result.FailedStage,report.Result.Detail);
        return Task.CompletedTask;
    }

    public void Dispose() => factory.Dispose();
}
