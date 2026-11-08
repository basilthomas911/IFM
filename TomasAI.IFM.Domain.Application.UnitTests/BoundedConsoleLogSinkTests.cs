using System.Collections.Concurrent;
using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using TomasAI.IFM.Application.Api.Server.Core.Observability.Logging;

namespace TomasAI.IFM.Domain.Application.UnitTests;

public sealed class BoundedConsoleLogSinkTests
{
    static LogEvent Event(LogEventLevel level) => new(DateTimeOffset.UtcNow, level, null,
        new MessageTemplateParser().Parse("fixture"), []);

    [Fact]
    public void Blocked_console_has_bounded_admission_separate_critical_capacity_and_shutdown()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var finished = new ManualResetEventSlim();
        var received = new ConcurrentQueue<LogEventLevel>();
        var target = new Sink(item => { entered.Set(); release.Wait(); received.Enqueue(item.Level); });
        var sink = new BoundedConsoleLogSink(target, 2, 1, TimeSpan.FromMilliseconds(100), new Cleanup(finished.Set));
        try
        {
            sink.Emit(Event(LogEventLevel.Information));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            for (var i = 0; i < 100; i++) sink.Emit(Event(LogEventLevel.Information));
            sink.Emit(Event(LogEventLevel.Error)); sink.Emit(Event(LogEventLevel.Error));
            Assert.Equal(3, sink.Pending);
            Assert.Equal(98, sink.DroppedRoutine);
            Assert.Equal(1, sink.DroppedCritical);
            var clock = Stopwatch.StartNew(); sink.Dispose();
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2));
            release.Set();
            Assert.True(finished.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(new[] { LogEventLevel.Information, LogEventLevel.Error, LogEventLevel.Information, LogEventLevel.Information }, received.ToArray());
            Assert.Equal(4, sink.Delivered);
        }
        finally { release.Set(); sink.Dispose(); finished.Wait(TimeSpan.FromSeconds(5)); }
    }

    [Fact]
    public void Sink_failure_does_not_stop_delivery_and_dispose_flushes_accepted_events()
    {
        var count = 0;
        var sink = new BoundedConsoleLogSink(new Sink(_ => { if (Interlocked.Increment(ref count) == 1) throw new IOException("fixture"); }), 100);
        for (var i = 0; i < 50; i++) sink.Emit(Event(LogEventLevel.Warning));
        sink.Dispose(); sink.Dispose();
        Assert.Equal(49, sink.Delivered);
        Assert.Equal(1, sink.Failed);
        Assert.Equal(0, sink.Pending);
        sink.Emit(Event(LogEventLevel.Error));
        Assert.Equal(1, sink.DroppedCritical);
    }

    sealed class Sink(Action<LogEvent> emit) : ILogEventSink { public void Emit(LogEvent logEvent) => emit(logEvent); }
    sealed class Cleanup(Action action) : IDisposable { public void Dispose() => action(); }
}
