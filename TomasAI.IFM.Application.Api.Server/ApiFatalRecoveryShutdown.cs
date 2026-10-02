using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.Api.Server;

public sealed record FatalRecoveryReport(
    DateTimeOffset OccurredUtc,
    string Reason,
    DatabentoHardRecoveryRequest Request,
    DatabentoHardRecoveryResult Result);

public sealed record FatalRecoveryShutdownOptions
{
    public TimeSpan GracefulTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan TelemetryTimeout { get; init; } = TimeSpan.FromSeconds(2);
    public int ExitCode { get; init; } = 42;

    public FatalRecoveryShutdownOptions Validate()
    {
        if (GracefulTimeout <= TimeSpan.Zero || GracefulTimeout > TimeSpan.FromMinutes(5)
            || TelemetryTimeout <= TimeSpan.Zero || TelemetryTimeout >= GracefulTimeout
            || ExitCode == 0)
            throw new InvalidOperationException("Fatal recovery shutdown policy must be finite and nonzero.");
        return this;
    }
}

public interface IRecoveryFatalTelemetry
{
    Task EmitAsync(FatalRecoveryReport report, CancellationToken cancellationToken);
}

public interface IApiFatalRecoveryShutdown
{
    bool IsRequested { get; }
    Task RequestAsync(FatalRecoveryReport report);
    void MarkGracefulShutdownComplete();
    void FailAndExit(Exception exception);
}

/// <summary>Owns one fatal shutdown request and an independent process deadline guard.</summary>
public sealed class ApiFatalRecoveryShutdown : IApiFatalRecoveryShutdown
{
    readonly IHostApplicationLifetime lifetime;
    readonly ILogger<ApiFatalRecoveryShutdown> logger;
    readonly IRecoveryFatalTelemetry telemetry;
    readonly FatalRecoveryShutdownOptions options;
    readonly TextWriter stdout;
    readonly TextWriter stderr;
    readonly Action<int> terminate;
    readonly ManualResetEventSlim completed = new(false);
    readonly object gate = new();
    FatalRecoveryReport? report;
    Task? requestedTask;

    public ApiFatalRecoveryShutdown(IHostApplicationLifetime lifetime,
        ILogger<ApiFatalRecoveryShutdown> logger, IRecoveryFatalTelemetry telemetry,
        FatalRecoveryShutdownOptions options)
        : this(lifetime, logger, telemetry, options, Console.Out, Console.Error,
            static code => Environment.Exit(code)) { }

    public ApiFatalRecoveryShutdown(IHostApplicationLifetime lifetime,
        ILogger<ApiFatalRecoveryShutdown> logger, IRecoveryFatalTelemetry telemetry,
        FatalRecoveryShutdownOptions options, TextWriter stdout, TextWriter stderr,
        Action<int> terminate)
    {
        this.lifetime = lifetime;
        this.logger = logger;
        this.telemetry = telemetry;
        this.options = options.Validate();
        this.stdout = stdout;
        this.stderr = stderr;
        this.terminate = terminate;
    }

    public bool IsRequested => Volatile.Read(ref report) is not null;

    public Task RequestAsync(FatalRecoveryReport fatal)
    {
        ArgumentNullException.ThrowIfNull(fatal);
        lock (gate)
        {
            if (requestedTask is not null) return requestedTask;
            fatal = Normalize(fatal);
            report = fatal;
            Environment.ExitCode = options.ExitCode;
            StartDeadlineGuard(fatal);
            return requestedTask = RequestCoreAsync(fatal);
        }
    }

    async Task RequestCoreAsync(FatalRecoveryReport fatal)
    {
        var summary = Summary(fatal);
        try { logger.LogCritical("API shutting down after unrecoverable Databento recovery: {Report}", summary); }
        catch (Exception) { }
        try { stdout.WriteLine(summary); stdout.Flush(); }
        catch (Exception) { }
        try
        {
            using var timeout = new CancellationTokenSource(options.TelemetryTimeout);
            var emission = telemetry.EmitAsync(fatal, timeout.Token);
            await emission.WaitAsync(options.TelemetryTimeout).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            try { stderr.WriteLine("Fatal recovery telemetry emission failed: " + exception.GetType().Name); }
            catch (Exception) { }
        }
        try { lifetime.StopApplication(); }
        catch (Exception exception) { FailAndExit(exception); }
    }

    public void MarkGracefulShutdownComplete()
    {
        if (!IsRequested) return;
        Environment.ExitCode = options.ExitCode;
        completed.Set();
    }

    public void FailAndExit(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var fatal = Volatile.Read(ref report);
        if (fatal is null) return;
        WriteStderr("API fatal recovery shutdown threw: " + Summary(fatal)
            + "; exception=" + Bound(exception.ToString(), 2048));
        Environment.ExitCode = options.ExitCode;
        try { terminate(options.ExitCode); }
        catch (Exception terminationFailure)
        {
            WriteStderr("API process termination failed: " + terminationFailure);
        }
    }

    void StartDeadlineGuard(FatalRecoveryReport fatal)
    {
        var guard = new Thread(() =>
        {
            try
            {
                if (completed.Wait(options.GracefulTimeout)) return;
                WriteStderr("API graceful shutdown timed out: " + Summary(fatal));
                Environment.ExitCode = options.ExitCode;
                terminate(options.ExitCode);
            }
            catch (Exception exception)
            {
                WriteStderr("API fatal shutdown guard failed: " + Bound(exception.ToString(), 2048));
            }
        })
        {
            IsBackground = true,
            Name = "IFM fatal recovery shutdown guard"
        };
        guard.Start();
    }

    void WriteStderr(string message)
    {
        try { stderr.WriteLine(message); stderr.Flush(); }
        catch (Exception) { }
    }

    string Summary(FatalRecoveryReport fatal)
    {
        var attempts = string.Join("; ", fatal.Result.AttemptEvidence.Take(3).Select(item =>
            $"attempt={item.Attempt},stage={item.Stage},safe={item.SafeToRetry},"
            + $"id={item.AttemptId},elapsedMs={item.Elapsed.TotalMilliseconds:F0},"
            + $"failure={item.FailureType}:{item.Detail},"
            + $"cleanup={item.CleanupFailureType}:{item.CleanupDetail}"));
        var message = $"Unrecoverable Databento recovery; reason={fatal.Reason}; "
            + $"correlation={fatal.Request.CorrelationId}; valueDate={fatal.Request.ValueDate}; "
            + $"attempts={fatal.Result.Attempts}; stage={fatal.Result.FailedStage}; "
            + $"detail={fatal.Result.Detail}; exitCode={options.ExitCode}; "
            + $"shutdownDeadlineMs={options.GracefulTimeout.TotalMilliseconds:F0}; {attempts}";
        return Bound(message, 8192);
    }

    static FatalRecoveryReport Normalize(FatalRecoveryReport fatal) => fatal with
    {
        Reason = Bound(fatal.Reason, 2048),
        Request = fatal.Request with
        {
            Source = Bound(fatal.Request.Source, 128),
            Reason = Bound(fatal.Request.Reason, 2048)
        },
        Result = fatal.Result with
        {
            FailedStage = Bound(fatal.Result.FailedStage, 128),
            Detail = Bound(fatal.Result.Detail, 2048),
            ContributingReasons = Array.AsReadOnly(fatal.Result.ContributingReasons.Take(16)
                .Select(item => Bound(item, 256)).ToArray()),
            AttemptEvidence = Array.AsReadOnly(fatal.Result.AttemptEvidence.Take(3)
                .Select(item => item with
                {
                    Stage = Bound(item.Stage, 128),
                    FailureType = Bound(item.FailureType, 128),
                    Detail = Bound(item.Detail, 2048),
                    CleanupFailureType = Bound(item.CleanupFailureType, 128),
                    CleanupDetail = Bound(item.CleanupDetail, 2048)
                }).ToArray())
        }
    };

    static string Bound(string value, int maximum)
    {
        value ??= string.Empty;
        var key = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
        if (!string.IsNullOrEmpty(key)) value = value.Replace(key, "[redacted]", StringComparison.Ordinal);
        return value.Length <= maximum ? value : value[..maximum];
    }
}
