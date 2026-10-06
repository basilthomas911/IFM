using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using TomasAI.IFM.Framework.Telemetry.Logging;
using TomasAI.IFM.Framework.Telemetry.Metrics;

var runId = Guid.NewGuid().ToString();
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Telemetry:Logs:OtlpEndpoint"] = "http://localhost:4318/v1/logs",
    ["Telemetry:Metrics:Enabled"] = "true",
    ["Telemetry:Metrics:OtlpEndpoint"] = "http://localhost:4317",
    ["Telemetry:Metrics:OtlpProtocol"] = "grpc",
    ["Telemetry:Traces:Enabled"] = "false"
}).Build();
var services = new ServiceCollection();
services.AddIfmMetrics(config, "IFM.DotNet.Telemetry.SmokeTest");
using var provider = services.BuildServiceProvider();
if (provider.GetService<TracerProvider>() is not null) throw new Exception("Trace export must be disabled.");
var metrics = provider.GetRequiredService<MeterProvider>();
using var meter = new Meter("TomasAI.IFM.Logging");
var gauge = meter.CreateObservableGauge("ifm_dotnet_smoke", () => 42d);
using var sink = new OtlpStructuredLogSink(config, "IFM.DotNet.Telemetry.SmokeTest");
using (var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger())
    logger.Information("{Component}.{Method} RunId={RunId} Quantity={Quantity}", "SmokeTest", "VerifyIngestion", runId, 10);
if (!metrics.ForceFlush(10000)) throw new Exception("Metric exporter did not flush.");
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
async Task<JsonElement> Query(string database, string sql)
{
    var text = await http.GetStringAsync($"http://localhost:4000/v1/sql?db={Uri.EscapeDataString(database)}&sql={Uri.EscapeDataString(sql)}");
    var data = JsonSerializer.Deserialize<JsonElement>(text);
    if (!data.TryGetProperty("output", out _)) throw new Exception(text);
    return data;
}
for (var attempt = 0; attempt < 15; attempt++)
{
    var logs = await Query("ifm_logs", "SELECT log_attributes FROM ifm_logs ORDER BY timestamp DESC LIMIT 100");
    if (logs.ToString().Contains(runId, StringComparison.Ordinal)) break;
    if (attempt == 14) throw new Exception(".NET log was not stored.");
    await Task.Delay(1000);
}
var values = await Query("ifm_metrics", "SELECT greptime_value FROM ifm_dotnet_smoke LIMIT 1");
if (values.GetProperty("output")[0].GetProperty("records").GetProperty("rows")[0][0].GetDouble() != 42d)
    throw new Exception(".NET metric was not stored correctly.");
foreach (var (database, ttl) in new[] { ("ifm_logs", "5days"), ("ifm_metrics", "30days") })
{
    var definition = await Query(database, "SHOW CREATE DATABASE " + database);
    if (!definition.ToString().Contains(ttl, StringComparison.Ordinal)) throw new Exception("Database TTL mismatch.");
}
Console.WriteLine($"PASS: .NET OTLP HTTP logs + gRPC metrics persisted; TTLs 5/30 days; trace provider disabled; run={runId}");
