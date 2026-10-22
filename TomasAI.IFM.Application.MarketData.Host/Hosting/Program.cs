using TomasAI.IFM.Application.MarketData.Host;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
var builder = WebApplication.CreateBuilder(args);
var apiConfiguration = builder.Configuration["MarketDataHost:ApiConfigurationDirectory"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "TomasAI.IFM.Application.Api.Server");
// Import backend settings without inheriting the API's Kestrel listeners or host lifecycle.
var backendConfiguration = new ConfigurationBuilder()
    .AddJsonFile(Path.Combine(apiConfiguration, "appsettings.json"), optional: true)
    .AddJsonFile(Path.Combine(apiConfiguration, "appsettings." + builder.Environment.EnvironmentName + ".json"), optional: true)
    .Build();
builder.Configuration.AddInMemoryCollection(backendConfiguration.AsEnumerable().Where(pair =>
    pair.Key.StartsWith("ConnectionStrings:", StringComparison.OrdinalIgnoreCase)
    || pair.Key.StartsWith("Storage:", StringComparison.OrdinalIgnoreCase)
    || pair.Key.StartsWith("AppSettings:", StringComparison.OrdinalIgnoreCase)
    || pair.Key.StartsWith("MarketDataRecovery:", StringComparison.OrdinalIgnoreCase)))
    .AddEnvironmentVariables().AddCommandLine(args);
if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]) && string.IsNullOrWhiteSpace(builder.Configuration["HTTP_PORTS"]))
    builder.WebHost.UseUrls("http://127.0.0.1:22544");
builder.Services.AddSingleton<NatsConnectionManager>();
builder.Services.AddSingleton(provider => new MarketDataHostConnection(provider.GetRequiredService<NatsConnectionManager>(),
    builder.Configuration["MarketDataHost:NatsUrl"] ?? "nats://127.0.0.1:4222",
    builder.Configuration["MarketDataHost:Scope"] ?? "development"));
if (!builder.Configuration.GetValue<bool>("MarketDataHost:ControlOnly")) builder.Services.AddMarketDataBackend(builder.Configuration);
else if (builder.Configuration["MarketDataHost:DataSource"] != "Synthetic")
    throw new InvalidOperationException("The control-only Host is restricted to isolated synthetic qualification.");
builder.Services.AddSingleton<MarketDataWorkerHost>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<MarketDataWorkerHost>());
builder.Services.AddHostedService<MarketDataNatsService>();
if (!builder.Configuration.GetValue<bool>("MarketDataHost:ControlOnly")) builder.Services.AddHostedService<MarketDataDatasetMonitor>();
var app = builder.Build();
app.MapGet("/health/live", () => Results.Ok(new { Status = "Healthy" }));
app.MapGet("/health/ready", async (MarketDataWorkerHost host, MarketDataHostConnection connection, CancellationToken token) =>
{
    if (!host.IsReady) return Results.StatusCode(503);
    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(1));
    try { return await connection.PostAsync<bool>("ready", new { }, deadline.Token).ConfigureAwait(false)
        ? Results.Ok(new { Status = "Ready" }) : Results.StatusCode(503); }
    catch (Exception) when (!token.IsCancellationRequested) { return Results.StatusCode(503); }
});
if (builder.Configuration.GetValue<bool>("MarketDataHost:StdinShutdown"))
    app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(async () =>
    {
        while (await Console.In.ReadLineAsync().ConfigureAwait(false) is { } command)
            if (string.Equals(command, "shutdown", StringComparison.Ordinal)) { app.Lifetime.StopApplication(); return; }
        app.Lifetime.StopApplication();
    }));
await app.RunAsync();
namespace TomasAI.IFM.Application.MarketData.Host
{
    public sealed class MarketDataHostAssemblyMarker;
}
