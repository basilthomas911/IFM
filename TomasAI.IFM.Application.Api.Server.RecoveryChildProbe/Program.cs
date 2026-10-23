using TomasAI.IFM.Application.Api.Server.Core.Recovery.Shutdown;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

if (args.Length == 4 && args[0] == "observe-recovery-error")
{
    await OptionRecoveryFailureInspection.RunAsync(args[1], Guid.Parse(args[2]), args[3]);
    return;
}

if (args.Length == 4 && args[0] == "inspect-option-baseline")
{
    await OptionBaselineInspection.RunAsync(args[1], DateOnly.ParseExact(args[2], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), args[3]);
    return;
}

if (args.Length == 4 && args[0] == "observe-option-notification")
{
    await OptionNotificationVerification.RunAsync(args[1], args[2], DateOnly.ParseExact(args[3], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
    return;
}

if (args.Length == 2 && args[0] == "host-action-failure")
{
    await HostRecoveryVerificationRunner.RunAsync(args[0], args[1]);
    return;
}

if (args.Length == 1 && args[0] is "host-terminal" or "host-console-throw" or "host-console-hang")
{
    await HostRecoveryVerificationRunner.RunAsync(args[0]);
    return;
}

if (args.Length != 1 || args[0] is not ("graceful" or "hung" or "throw"))
    throw new ArgumentException("Expected graceful, hung, or throw mode.");

var lifetime = new ProbeLifetime(args[0] == "throw");
var shutdown = new ApiFatalRecoveryShutdown(lifetime,
    NullLogger<ApiFatalRecoveryShutdown>.Instance, new ProbeTelemetry(),
    new() { GracefulTimeout = TimeSpan.FromMilliseconds(500),
        TelemetryTimeout = TimeSpan.FromMilliseconds(100), ExitCode = 42 });
var request = new DatabentoHardRecoveryRequest(Guid.NewGuid(),
    new DateOnly(2026, 9, 30), Guid.NewGuid(), "ChildProbe", "Injected failure");
var result = new DatabentoHardRecoveryResult(request.CorrelationId, Guid.Empty,
    3, DatabentoHardRecoveryOutcome.Unrecoverable, "LocalQualification", "No healthy workers");
await shutdown.RequestAsync(new(DateTimeOffset.UtcNow, "Three hard attempts failed", request, result));
if (args[0] == "graceful")
{
    try { await Task.Delay(TimeSpan.FromSeconds(1), lifetime.ApplicationStopping); }
    catch (OperationCanceledException) { }
    shutdown.MarkGracefulShutdownComplete();
}
else await Task.Delay(TimeSpan.FromSeconds(3));

sealed class ProbeLifetime(bool throwOnStop) : IHostApplicationLifetime
{
    readonly CancellationTokenSource stopping = new();
    public CancellationToken ApplicationStarted => CancellationToken.None;
    public CancellationToken ApplicationStopping => stopping.Token;
    public CancellationToken ApplicationStopped => CancellationToken.None;
    public void StopApplication()
    {
        if (throwOnStop) throw new InvalidOperationException("Injected host stop failure");
        stopping.Cancel();
    }
}

sealed class ProbeTelemetry : IRecoveryFatalTelemetry
{
    public Task EmitAsync(FatalRecoveryReport report, CancellationToken cancellationToken)
    {
        Console.WriteLine("OTEL_LOCAL_SUBMISSION=" + report.Request.CorrelationId);
        return Task.CompletedTask;
    }
}
