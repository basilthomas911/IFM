using Microsoft.Extensions.Logging;
using TomasAI.IFM.UI.Net.Views.Presentation;

namespace TomasAI.IFM.UI.Net.SystemTests.Diagnostics;

public sealed class UiExceptionReporterTests
{
    [Fact]
    public async Task Observe_records_faulted_fire_and_forget_task()
    {
        var logger = new CapturingLogger();
        UiExceptionReporter.Configure(logger);

        UiExceptionReporter.Observe(
            Task.FromException(new InvalidOperationException("controlled failure")),
            "ControlledOperation");

        var entry = await logger.Entry.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Contains("ControlledOperation", entry.Message, StringComparison.Ordinal);
        Assert.Contains("FireAndForgetTask", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Observe_does_not_log_expected_cancellation()
    {
        var logger = new CapturingLogger();
        UiExceptionReporter.Configure(logger);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        UiExceptionReporter.Observe(
            Task.FromCanceled(cancellation.Token),
            "CancelledOperation",
            cancellationToken: cancellation.Token);

        await Task.Delay(100);
        Assert.False(logger.Entry.Task.IsCompleted);
    }

    [Fact]
    public void Report_never_throws_when_logger_fails()
    {
        UiExceptionReporter.Configure(new ThrowingLogger());

        var exception = Record.Exception(() => UiExceptionReporter.Report(
            new InvalidOperationException("controlled logger failure"),
            "Test",
            "Report"));

        Assert.Null(exception);
    }

    sealed class CapturingLogger : ILogger
    {
        public TaskCompletionSource<(Exception? Exception, string Message)> Entry { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entry.TrySetResult((exception, formatter(state, exception)));
    }

    sealed class ThrowingLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("logger unavailable");
    }
}
