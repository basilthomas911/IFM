using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Serilog.Core;
using Serilog.Events;

namespace TomasAI.IFM.Application.Api.Server.Core.Observability.Logging;

/// <summary>Isolates a stalled console from request threads with separately bounded critical and routine queues.</summary>
public sealed class BoundedConsoleLogSink : ILogEventSink, IDisposable
{
    readonly Channel<LogEvent> routine;
    readonly Channel<LogEvent> critical;
    readonly ILogEventSink destination;
    readonly IDisposable? owner;
    readonly SemaphoreSlim available = new(0);
    readonly object admission = new();
    readonly Thread worker;
    readonly TimeSpan shutdownBudget;
    readonly Meter meter = new("TomasAI.IFM.Logging.Console", "1.0");
    bool stopped;
    long droppedRoutine, droppedCritical, delivered, failed;

    public long DroppedRoutine => Interlocked.Read(ref droppedRoutine);
    public long DroppedCritical => Interlocked.Read(ref droppedCritical);
    public long Delivered => Interlocked.Read(ref delivered);
    public long Failed => Interlocked.Read(ref failed);
    public int Pending => routine.Reader.Count + critical.Reader.Count;

    public BoundedConsoleLogSink(ILogEventSink destination, int capacity = 4096, int criticalCapacity = 256,
        TimeSpan? shutdownBudget = null, IDisposable? owner = null)
    {
        if (capacity < 1 || criticalCapacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.shutdownBudget = shutdownBudget ?? TimeSpan.FromSeconds(2);
        if (this.shutdownBudget < TimeSpan.Zero || this.shutdownBudget.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(shutdownBudget));
        this.destination = destination;
        this.owner = owner;
        routine = Create(capacity);
        critical = Create(criticalCapacity);
        meter.CreateObservableGauge("console.logs.pending", () => Pending);
        meter.CreateObservableCounter("console.logs.dropped.routine", () => DroppedRoutine);
        meter.CreateObservableCounter("console.logs.dropped.critical", () => DroppedCritical);
        meter.CreateObservableCounter("console.logs.delivered", () => Delivered);
        meter.CreateObservableCounter("console.logs.failed", () => Failed);
        worker = new Thread(Consume) { IsBackground = true, Name = "IFM bounded console" };
        worker.Start();
    }

    static Channel<LogEvent> Create(int capacity) => Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(capacity)
    { SingleReader = true, FullMode = BoundedChannelFullMode.Wait, AllowSynchronousContinuations = false });

    public void Emit(LogEvent logEvent)
    {
        var important = logEvent.Level >= LogEventLevel.Warning;
        lock (admission)
        {
            if (!stopped && (important ? critical : routine).Writer.TryWrite(logEvent))
            {
                available.Release();
                return;
            }
            if (important) Interlocked.Increment(ref droppedCritical);
            else Interlocked.Increment(ref droppedRoutine);
        }
    }

    void Consume()
    {
        try
        {
            while (true)
            {
                available.Wait();
                if (critical.Reader.TryRead(out var item) || routine.Reader.TryRead(out item))
                {
                    try { destination.Emit(item); Interlocked.Increment(ref delivered); }
                    catch { Interlocked.Increment(ref failed); }
                }
                else { lock (admission) { if (stopped) return; } }
            }
        }
        finally
        {
            try { owner?.Dispose(); }
            catch { Interlocked.Increment(ref failed); }
            finally { meter.Dispose(); available.Dispose(); }
        }
    }

    /// <summary>Drains accepted events within the budget; a blocked console can finish on its background thread later.</summary>
    public void Dispose()
    {
        lock (admission)
        {
            if (stopped) return;
            stopped = true;
            routine.Writer.TryComplete(); critical.Writer.TryComplete();
            available.Release();
        }
        if (Thread.CurrentThread != worker) worker.Join(shutdownBudget);
    }
}
