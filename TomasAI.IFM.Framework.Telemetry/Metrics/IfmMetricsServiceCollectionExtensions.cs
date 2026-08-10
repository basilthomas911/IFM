using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace TomasAI.IFM.Framework.Telemetry.Metrics;

/// <summary>Registers the process-wide IFM metrics pipeline and OTLP exporter.</summary>
public static class IfmMetricsServiceCollectionExtensions
{
    const string ActorMeterName = "TomasAI.IFM.Shared.EventModelActor";
    const string NatsMeterName = "TomasAI.IFM.Framework.Messaging.Nats";
    const string EventProjectorMeterName = "TomasAI.IFM.Application.EventProjector";

    /// <summary>
    /// Adds IFM, .NET runtime, ASP.NET Core, Kestrel, and HTTP client metrics when
    /// <c>Telemetry:Metrics:Enabled</c> is true.
    /// </summary>
    public static IServiceCollection AddIfmMetrics(
        this IServiceCollection services,
        IConfiguration configuration,
        string defaultServiceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultServiceName);

        var section = configuration.GetSection("Telemetry:Metrics");
        if (!section.GetValue("Enabled", false))
            return services;

        var serviceName = section.GetValue<string>("ServiceName");
        if (string.IsNullOrWhiteSpace(serviceName))
            serviceName = defaultServiceName;

        var endpointText = section.GetValue<string>("OtlpEndpoint");
        var protocolText = section.GetValue<string>("OtlpProtocol");

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(ActorMeterName)
                    .AddMeter(NatsMeterName)
                    .AddMeter(EventProjectorMeterName)
                    .AddMeter("System.Runtime")
                    .AddMeter("Microsoft.AspNetCore.Hosting")
                    .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                    .AddMeter("System.Net.Http")
                    .AddMeter("System.Net.NameResolution")
                    .AddOtlpExporter(options =>
                    {
                        if (!string.IsNullOrWhiteSpace(endpointText))
                        {
                            if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint))
                            {
                                throw new InvalidOperationException(
                                    $"Telemetry:Metrics:OtlpEndpoint '{endpointText}' is not an absolute URI.");
                            }

                            options.Endpoint = endpoint;
                        }

                        options.Protocol = string.Equals(
                            protocolText,
                            "http/protobuf",
                            StringComparison.OrdinalIgnoreCase)
                            ? OtlpExportProtocol.HttpProtobuf
                            : OtlpExportProtocol.Grpc;
                    });
            });

        return services;
    }
}
