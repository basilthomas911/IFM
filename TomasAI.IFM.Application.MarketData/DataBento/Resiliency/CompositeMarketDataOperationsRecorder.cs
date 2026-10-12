using TomasAI.IFM.Application.MarketData.MarketOutlook;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed class CompositeMarketDataOperationsRecorder(params IMarketDataOperationsRecorder[] recorders)
    : IMarketDataOperationsRecorder
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "A diagnostic recorder failure must not abort realtime ingestion or recovery; this boundary isolates the observer.")]
    public void Record(in MarketDataOperationMeasurement measurement)
    {
        foreach (var recorder in recorders)
            try { recorder.Record(measurement); } catch { /* Cancellation or optional observation must not abort the enclosing shutdown/recovery boundary. */ }
    }
}
