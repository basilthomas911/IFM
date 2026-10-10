using TomasAI.IFM.Application.Api.Server.Core.Recovery.Shutdown;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class ApiFatalRecoveryShutdownTests
{
    [Fact]
    public async Task Fatal_report_is_emitted_once_and_graceful_completion_keeps_nonzero_exit()
    {
        var previousExit = Environment.ExitCode;
        try
        {
            var lifetime = Substitute.For<IHostApplicationLifetime>();
            var telemetry = Substitute.For<IRecoveryFatalTelemetry>();
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exits = 0;
            var shutdown = new ApiFatalRecoveryShutdown(lifetime,
                NullLogger<ApiFatalRecoveryShutdown>.Instance, telemetry,
                new() { GracefulTimeout = TimeSpan.FromMilliseconds(300),
                    TelemetryTimeout = TimeSpan.FromMilliseconds(100), ExitCode = 42 },
                stdout, stderr, _ => Interlocked.Increment(ref exits));
            var report = Report();

            await shutdown.RequestAsync(report);
            await shutdown.RequestAsync(report);
            shutdown.MarkGracefulShutdownComplete();
            await Task.Delay(350);

            Assert.True(shutdown.IsRequested);
            Assert.Contains("Unrecoverable Databento recovery", stdout.ToString());
            Assert.Equal(42, Environment.ExitCode);
            Assert.Equal(0, exits);
            lifetime.Received(1).StopApplication();
            await telemetry.Received(1).EmitAsync(
                Arg.Is<FatalRecoveryReport>(item =>
                    item.Request.CorrelationId == report.Request.CorrelationId),
                Arg.Any<CancellationToken>());
        }
        finally { Environment.ExitCode = previousExit; }
    }

    [Fact]
    public async Task Hung_graceful_shutdown_writes_stderr_and_requests_nonzero_exit()
    {
        var previousExit = Environment.ExitCode;
        try
        {
            var exit = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stderr = new StringWriter();
            var shutdown = new ApiFatalRecoveryShutdown(Substitute.For<IHostApplicationLifetime>(),
                NullLogger<ApiFatalRecoveryShutdown>.Instance,
                Substitute.For<IRecoveryFatalTelemetry>(),
                new() { GracefulTimeout = TimeSpan.FromMilliseconds(150),
                    TelemetryTimeout = TimeSpan.FromMilliseconds(50), ExitCode = 42 },
                new StringWriter(), stderr, code => exit.TrySetResult(code));

            await shutdown.RequestAsync(Report());

            Assert.Equal(42, await exit.Task.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Contains("timed out", stderr.ToString());
        }
        finally { Environment.ExitCode = previousExit; }
    }

    [Fact]
    public async Task Shutdown_exception_uses_direct_stderr_and_exit_fallback()
    {
        var previousExit = Environment.ExitCode;
        try
        {
            var lifetime = Substitute.For<IHostApplicationLifetime>();
            lifetime.When(instance => instance.StopApplication())
                .Do(_ => throw new InvalidOperationException("Injected host stop failure"));
            var exit = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stderr = new StringWriter();
            var shutdown = new ApiFatalRecoveryShutdown(lifetime,
                NullLogger<ApiFatalRecoveryShutdown>.Instance,
                Substitute.For<IRecoveryFatalTelemetry>(),
                new() { GracefulTimeout = TimeSpan.FromMilliseconds(300),
                    TelemetryTimeout = TimeSpan.FromMilliseconds(100), ExitCode = 42 },
                new StringWriter(), stderr, code => exit.TrySetResult(code));

            await shutdown.RequestAsync(Report());
            shutdown.MarkGracefulShutdownComplete();

            Assert.Equal(42, await exit.Task.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Contains("Injected host stop failure", stderr.ToString());
        }
        finally { Environment.ExitCode = previousExit; }
    }

    [Fact]
    public async Task Logging_and_telemetry_failures_cannot_suppress_stdout_or_host_shutdown()
    {
        var previousExit = Environment.ExitCode;
        try
        {
            var lifetime = Substitute.For<IHostApplicationLifetime>();
            var telemetry = Substitute.For<IRecoveryFatalTelemetry>();
            telemetry.EmitAsync(Arg.Any<FatalRecoveryReport>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromException(new IOException("Injected telemetry failure")));
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var shutdown = new ApiFatalRecoveryShutdown(lifetime,
                new ThrowingLogger(), telemetry,
                new() { GracefulTimeout = TimeSpan.FromSeconds(2),
                    TelemetryTimeout = TimeSpan.FromMilliseconds(100), ExitCode = 42 },
                stdout, stderr, _ => throw new InvalidOperationException("Unexpected forced exit"));

            await shutdown.RequestAsync(Report());
            shutdown.MarkGracefulShutdownComplete();

            Assert.Contains("Unrecoverable Databento recovery", stdout.ToString());
            Assert.Contains("Fatal recovery telemetry emission failed", stderr.ToString());
            lifetime.Received(1).StopApplication();
            Assert.Equal(42, Environment.ExitCode);
        }
        finally { Environment.ExitCode = previousExit; }
    }

    [Fact]
    public async Task Fatal_report_redacts_secret_and_bounds_attempt_evidence_before_telemetry()
    {
        var previousExit = Environment.ExitCode;
        var previousKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
        const string secret = "fatal-recovery-test-secret";
        try
        {
            Environment.SetEnvironmentVariable("DATABENTO_API_KEY", secret);
            var telemetry = Substitute.For<IRecoveryFatalTelemetry>();
            FatalRecoveryReport? emitted = null;
            telemetry.EmitAsync(Arg.Do<FatalRecoveryReport>(item => emitted = item),
                    Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            var stdout = new StringWriter();
            var shutdown = new ApiFatalRecoveryShutdown(Substitute.For<IHostApplicationLifetime>(),
                NullLogger<ApiFatalRecoveryShutdown>.Instance, telemetry,
                new() { GracefulTimeout = TimeSpan.FromSeconds(2),
                    TelemetryTimeout = TimeSpan.FromMilliseconds(100), ExitCode = 42 },
                stdout, new StringWriter(), _ => throw new InvalidOperationException("Unexpected exit"));
            var report = Report() with
            {
                Reason = $"Failure {secret}",
                Result = Report().Result with
                {
                    AttemptEvidence = Enumerable.Range(1, 10)
                        .Select(index => new DatabentoHardAttemptEvidence(index, false,
                            "Connect", "IOException", $"Failure {secret}", []))
                        .ToArray()
                }
            };

            await shutdown.RequestAsync(report);
            shutdown.MarkGracefulShutdownComplete();

            Assert.NotNull(emitted);
            Assert.DoesNotContain(secret, stdout.ToString());
            Assert.DoesNotContain(secret, emitted!.Reason);
            Assert.Equal(3, emitted.Result.AttemptEvidence.Count);
            Assert.All(emitted.Result.AttemptEvidence,
                item => Assert.DoesNotContain(secret, item.Detail));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DATABENTO_API_KEY", previousKey);
            Environment.ExitCode = previousExit;
        }
    }

    [Fact]
    public async Task Stdout_failure_does_not_suppress_telemetry_or_host_stop()
    {
        var previousExit = Environment.ExitCode;
        try
        {
            var lifetime = Substitute.For<IHostApplicationLifetime>();
            var telemetry = Substitute.For<IRecoveryFatalTelemetry>();
            var report = Report();
            var shutdown = new ApiFatalRecoveryShutdown(lifetime,
                NullLogger<ApiFatalRecoveryShutdown>.Instance, telemetry,
                new() { GracefulTimeout = TimeSpan.FromSeconds(2),
                    TelemetryTimeout = TimeSpan.FromMilliseconds(100), ExitCode = 42 },
                new ThrowingWriter(), new StringWriter(),
                _ => throw new InvalidOperationException("Unexpected forced exit"));

            await shutdown.RequestAsync(report);
            shutdown.MarkGracefulShutdownComplete();

            await telemetry.Received(1).EmitAsync(
                Arg.Is<FatalRecoveryReport>(item =>
                    item.Request.CorrelationId == report.Request.CorrelationId),
                Arg.Any<CancellationToken>());
            lifetime.Received(1).StopApplication();
        }
        finally { Environment.ExitCode = previousExit; }
    }

    sealed class ThrowingWriter : StringWriter
    {
        public override void WriteLine(string? value) => throw new IOException("Injected stdout failure");
    }

    sealed class ThrowingLogger : ILogger<ApiFatalRecoveryShutdown>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            throw new IOException("Injected logger failure");

        sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    static FatalRecoveryReport Report()
    {
        var request = new DatabentoHardRecoveryRequest(Guid.NewGuid(),
            new DateOnly(2026, 9, 30), Guid.NewGuid(), "Test", "Worker failure");
        var result = new DatabentoHardRecoveryResult(request.CorrelationId, Guid.Empty, 3,
            DatabentoHardRecoveryOutcome.Unrecoverable, "LocalQualification", "No records")
        {
            AttemptEvidence = [new(3, true, "LocalQualification", "TimeoutException",
                "No records", [123])]
        };
        return new(DateTimeOffset.UtcNow, "Three failed hard attempts", request, result);
    }
}
