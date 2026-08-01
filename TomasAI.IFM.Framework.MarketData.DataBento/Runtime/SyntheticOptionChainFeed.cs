using System.Text;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal sealed class SyntheticOptionChainFeed : IDatabentoOptionChainFeed
{
    private readonly SyntheticTickerFeed _inner;
    private InstrumentKey? _firstInstrument;
    private bool _subscribed;

    internal SyntheticOptionChainFeed(DatabentoFeedOptions options)
    {
        _inner = new SyntheticTickerFeed(options, singleChannel: true);
    }

    public ISynchronousBatchReader<MarketDataBatch64> Reader
    {
        get
        {
            if (_firstInstrument is not { } instrument)
            {
                throw new InvalidOperationException("Start the option-chain feed before requesting its reader.");
            }
            return _inner.GetReader(instrument);
        }
    }

    public void Subscribe(OptionChainSubscription subscription, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription.Underlying);
        if (_subscribed)
        {
            throw new InvalidOperationException("The option-chain subscription is immutable.");
        }
        if (subscription.ResolvedContracts.Count == 0)
        {
            throw new ArgumentException("At least one resolved option contract is required.");
        }
        var keys = new HashSet<InstrumentKey>();
        for (var index = 0; index < subscription.ResolvedContracts.Count; index++)
        {
            var contract = subscription.ResolvedContracts[index];
            ArgumentException.ThrowIfNullOrWhiteSpace(contract.RawSymbol);
            if (Encoding.UTF8.GetByteCount(contract.RawSymbol) > ushort.MaxValue)
            {
                throw new ArgumentException(
                    "Option symbols cannot exceed 65,535 UTF-8 bytes.");
            }
            if (!keys.Add(contract.Instrument))
            {
                throw new ArgumentException($"Duplicate option instrument {contract.Instrument}.");
            }
        }
        _inner.SubscribeOptionChain(
            subscription.ResolvedContracts,
            subscription.DataKinds,
            timeout);
        _subscribed = true;
    }

    public void Start(TimeSpan timeout)
    {
        _inner.Start(timeout);
        _firstInstrument = _inner.GetInstruments()[0].Instrument;
    }

    public void Stop(TimeSpan timeout) => _inner.Stop(timeout);

    public FeedHealthSnapshot GetHealth() => _inner.GetHealth();

    public void Dispose() => _inner.Dispose();
}
