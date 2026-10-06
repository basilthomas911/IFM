using System.Diagnostics.Metrics;
using Serilog.Sinks.Async;
using TomasAI.IFM.Framework.Telemetry.Logging;
using Xunit;

namespace TomasAI.IFM.Framework.Telemetry.UnitTests;

public sealed class AsyncLogBufferMonitorTests
{
    [Fact]
    public void ReportsBacklogCapacityAndDroppedRecordsWithoutLogging()
    {
        var readings = new Dictionary<string, long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Meter.Name == "TomasAI.IFM.Logging") owner.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<int>((instrument, value, _, _) => readings[instrument.Name] = value);
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => readings[instrument.Name] = value);
        listener.Start();
        var monitor = new AsyncLogBufferMonitor();
        var inspector = new Inspector();
        monitor.StartMonitoring(inspector);
        listener.RecordObservableInstruments();
        Assert.Equal(12, readings["logging.file.pending"]);
        Assert.Equal(4096, readings["logging.file.capacity"]);
        Assert.Equal(3, readings["logging.file.dropped"]);
        monitor.StopMonitoring(inspector);
    }
    sealed class Inspector : IAsyncLogEventSinkInspector
    {
        public int BufferSize => 4096;
        public int Count => 12;
        public long DroppedMessagesCount => 3;
    }
}
