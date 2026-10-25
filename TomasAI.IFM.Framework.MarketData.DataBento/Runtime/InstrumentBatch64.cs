using TomasAI.IFM.Shared.MarketData.Definitions;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>Initializes a new InstrumentBatch64 instance.</summary>
/// <param name="Instrument">The instrument.</param>
/// <param name="Batch">The batch.</param>
public readonly record struct InstrumentBatch64(
    InstrumentKey Instrument,
    MarketDataBatch64 Batch) : IDisposable
{
    /// <summary>Releases the resources owned by this instance.</summary>
    public void Dispose() => Batch.Dispose();
}
