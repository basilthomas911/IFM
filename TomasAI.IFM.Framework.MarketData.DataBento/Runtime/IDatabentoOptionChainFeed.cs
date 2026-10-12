using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public interface IDatabentoOptionChainFeed : IDisposable
{
    void Subscribe(OptionChainSubscription subscription, TimeSpan timeout);
    /// <summary>
    /// Prepares the feed, invokes <paramref name="startConsumer"/> after the reader exists,
    /// and activates native ring publication only after that callback returns.
    /// </summary>
    void Start(TimeSpan timeout, Action<TimeSpan> startConsumer);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Retain the published C# interface member and named argument contract.")]
    void Stop(TimeSpan timeout);
    ISynchronousBatchReader<MarketDataBatch64> Reader { get; }
    FeedHealthSnapshot GetHealth();
}
