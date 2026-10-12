using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public interface IMultiplexedTickerBatchReader : IDisposable
{
    bool TryRead(out InstrumentBatch64 batch);
    bool TryRead(TimeSpan timeout, out InstrumentBatch64 batch);
    bool TryRead(TimeSpan timeout, CancellationToken cancellationToken, out InstrumentBatch64 batch)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return TryRead(timeout, out batch);
    }
    InstrumentBatch64 Read(TimeSpan timeout);
    bool IsCompleted { get; }
}
