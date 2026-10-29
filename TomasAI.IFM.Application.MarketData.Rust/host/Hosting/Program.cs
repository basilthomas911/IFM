using Serilog;
using TomasAI.IFM.Application.MarketData.Rust.Host;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;

// Same Host process/remote contract, separate opt-in output directory. No child
// process or legacy provider registration exists in this executable.
var builder=WebApplication.CreateBuilder(args);
var apiConfiguration=builder.Configuration["MarketDataHost:ApiConfigurationDirectory"]??Path.Combine(builder.Environment.ContentRootPath,"TomasAI.IFM.Application.Api.Server");
var backendConfiguration=new ConfigurationBuilder().AddJsonFile(Path.Combine(apiConfiguration,"appsettings.json"),optional:true)
    .AddJsonFile(Path.Combine(apiConfiguration,"appsettings."+builder.Environment.EnvironmentName+".json"),optional:true).Build();
builder.Configuration.AddInMemoryCollection(backendConfiguration.AsEnumerable().Where(pair=>pair.Key.StartsWith("ConnectionStrings:",StringComparison.OrdinalIgnoreCase)||pair.Key.StartsWith("Storage:",StringComparison.OrdinalIgnoreCase)||pair.Key.StartsWith("AppSettings:",StringComparison.OrdinalIgnoreCase)||pair.Key.StartsWith("MarketDataRecovery:",StringComparison.OrdinalIgnoreCase)||pair.Key.StartsWith("Telemetry:",StringComparison.OrdinalIgnoreCase)))
    .AddEnvironmentVariables().AddCommandLine(args);
if(string.IsNullOrWhiteSpace(builder.Configuration["urls"])&&string.IsNullOrWhiteSpace(builder.Configuration["HTTP_PORTS"]))builder.WebHost.UseUrls("http://127.0.0.1:22544");
builder.Logging.ClearProviders().AddSerilog(NativeHostLogging.Create(builder.Configuration),dispose:true);
builder.Services.Configure<HostOptions>(options=>options.ShutdownTimeout=TimeSpan.FromMinutes(2));
builder.Services.AddSingleton<NatsConnectionManager>();
builder.Services.AddSingleton(p=>new MarketDataHostConnection(p.GetRequiredService<NatsConnectionManager>(),builder.Configuration["MarketDataHost:NatsUrl"]??"nats://127.0.0.1:4222",builder.Configuration["MarketDataHost:Scope"]??"development"));
builder.Services.AddNativeMarketDataBackend(builder.Configuration);
await using var app=builder.Build();
app.MapGet("/health/live",()=>Results.Ok(new{Status="Healthy",Implementation="RustFFI"}));
app.MapGet("/health/ready",(NativeMarketDataNatsService service,NativeColdBackend cold)=>service.IsReady&&cold.IsHealthy?Results.Ok(new{Status="Ready",Implementation="RustFFI"}):Results.StatusCode(503));
if(builder.Configuration.GetValue<bool>("MarketDataHost:StdinShutdown"))app.Lifetime.ApplicationStarted.Register(()=>_=Task.Run(async()=>
{
    while(await Console.In.ReadLineAsync().ConfigureAwait(false) is{} command)if(string.Equals(command,"shutdown",StringComparison.Ordinal)){app.Lifetime.StopApplication();return;}
    app.Lifetime.StopApplication();
}));
await app.RunAsync();
