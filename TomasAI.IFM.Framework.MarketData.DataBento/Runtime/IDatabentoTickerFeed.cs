using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public interface IDatabentoTickerFeed : IDisposable
{
    void Subscribe(ReadOnlySpan<TickerSubscription> subscriptions, TimeSpan timeout);
    /// <summary>
    /// Prepares the feed, invokes <paramref name="startConsumer"/> after readers exist,
    /// and activates native ring publication only after that callback returns.
    /// </summary>
    void Start(TimeSpan timeout, Action<TimeSpan> startConsumer);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Retain the published C# interface member and named argument contract.")]
    void Stop(TimeSpan timeout);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Retain the published C# interface member and named argument contract.")]
    void Stop(TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stop(timeout);
    }
    ISynchronousBatchReader<MarketDataBatch64> GetReader(InstrumentKey instrument);
    IMultiplexedTickerBatchReader GetMultiplexedReader();
    IReadOnlyList<TickerInstrumentRegistration> GetInstruments();
    FeedHealthSnapshot GetHealth();
}
