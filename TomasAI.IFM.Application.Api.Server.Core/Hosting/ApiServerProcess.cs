using TomasAI.IFM.Application.Api.Server.Core.Recovery.Shutdown;
using Serilog;
namespace TomasAI.IFM.Application.Api.Server.Core.Hosting;

/// <summary>Process telemetry and failure diagnostics shared by normal and maintenance startup.</summary>
public static class ApiServerProcess
{
    public static IDisposable RecordGc(IConfiguration configuration) => TomasAI.IFM.Framework.Telemetry.Metrics.ProcessGcStatisticsRecorder.Start(configuration, "TomasAI.IFM.Application.Api.Server");
    public static void FlushLogs() => Log.CloseAndFlush();
    public static void ReportFailure(Exception ex, string[] args, IApiFatalRecoveryShutdown? fatalRecoveryShutdown = null)
    {

        if (fatalRecoveryShutdown?.IsRequested == true) fatalRecoveryShutdown.FailAndExit(ex);
        else Environment.ExitCode = 1;
        if (args.Contains("--publish-option-pricing-reference-only", StringComparer.OrdinalIgnoreCase)
        || args.Contains("--publish-oct1-option-pricing-reference-only", StringComparer.OrdinalIgnoreCase))
        {
            var detail = ex.Message;
            var key = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
            if (!string.IsNullOrEmpty(key)) detail = detail.Replace(key, "[redacted]", StringComparison.Ordinal);
            Console.Error.WriteLine("Option pricing reference publication failed: " + detail[..Math.Min(detail.Length, 2048)]);
        }
        if (args.Contains("--refresh-instrument-definitions-only", StringComparer.OrdinalIgnoreCase))
        Console.Error.WriteLine("Instrument definition refresh failed: " + ex.Message);
        Log.Fatal(ex, "IFM WebApiServer: startup failed");
        if (args.Contains("--verify-startup-only", StringComparer.OrdinalIgnoreCase))
        Console.Error.WriteLine("IFM startup verification failed: " + ex.Message);
        if (!args.Contains("--verify-startup-only", StringComparer.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IFM_TEST_NATS_URL")))
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();


    }
}
