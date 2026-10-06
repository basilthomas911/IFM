using System.Diagnostics.Metrics;
using Serilog.Sinks.Async;

namespace TomasAI.IFM.Framework.Telemetry.Logging;

/// <summary>Observable bounded file-sink health; callbacks do not log or block application threads.</summary>
public sealed class AsyncLogBufferMonitor : IAsyncLogEventSinkMonitor
{
    readonly Meter meter = new("TomasAI.IFM.Logging", "1.0");
    IAsyncLogEventSinkInspector? inspector;
    public AsyncLogBufferMonitor()
    {
        meter.CreateObservableGauge("logging.file.pending", () => Volatile.Read(ref inspector)?.Count ?? 0);
        meter.CreateObservableGauge("logging.file.capacity", () => Volatile.Read(ref inspector)?.BufferSize ?? 0);
        meter.CreateObservableCounter("logging.file.dropped", () => Volatile.Read(ref inspector)?.DroppedMessagesCount ?? 0);
    }
    public void StartMonitoring(IAsyncLogEventSinkInspector inspector) => Volatile.Write(ref this.inspector, inspector);
    public void StopMonitoring(IAsyncLogEventSinkInspector inspector) { Volatile.Write(ref this.inspector, null); meter.Dispose(); }
}
