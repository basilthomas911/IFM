using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using TomasAI.IFM.Framework.Telemetry.Logging;
using TomasAI.IFM.Framework.Telemetry.Metrics;
using TomasAI.IFM.Application.ScheduledTask.Shared;

namespace TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        var logging = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration);
        if (builder.Configuration.GetValue<bool>("Telemetry:Logs:Enabled"))
            logging.WriteTo.Sink(new OtlpStructuredLogSink(builder.Configuration, "IFM-ScheduledTask-FuturesMarketClose"));
        Log.Logger = logging.CreateLogger();
        builder.Services.AddIfmMetrics(builder.Configuration, "IFM-ScheduledTask-FuturesMarketClose");
        builder.Services.AddSerilog();

        builder.Services.AddHttpClient<TomasAI.IFM.Domain.SystemAdmin.Shared.MarketClose.IMarketCloseApi, MarketCloseClient>(client =>
        {
            client.BaseAddress = new Uri(builder.Configuration["ApiServer:BaseUrl"] ?? "http://localhost:22543/");
            client.Timeout = TimeSpan.FromMinutes(11);
        });
        builder.Services.AddScheduledTaskRuntime();
        builder.Services.AddHostedService<Worker>();

        using var host = builder.Build();
        var outcome = host.Services.GetRequiredService<ScheduledTaskOutcome>();
        await host.RunAsync().ConfigureAwait(false);
        return outcome.ExitCode;
    }
}
