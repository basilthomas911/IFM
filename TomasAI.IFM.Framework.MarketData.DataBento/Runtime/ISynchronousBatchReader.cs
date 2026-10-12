using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public interface ISynchronousBatchReader<TBatch>
    where TBatch : class, IDisposable
{
    bool TryRead(out TBatch? batch);
    bool TryRead(TimeSpan timeout, out TBatch? batch);
    TBatch Read(TimeSpan timeout);
    bool IsCompleted { get; }
}
