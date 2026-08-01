using System.Text;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal sealed class SyntheticOptionChainFeed : IDatabentoOptionChainFeed
{
    private readonly DatabentoFeedOptions _options;
    private readonly SyntheticTickerFeed _inner;
    private InstrumentKey? _firstInstrument;
    private bool _subscribed;

    internal SyntheticOptionChainFeed(DatabentoFeedOptions options)
    {
        _options = options;
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
        if (subscription.MaturityDate == DateOnly.MinValue)
        {
            throw new ArgumentException("An exact option maturity date is required.", nameof(subscription));
        }
        ArgumentNullException.ThrowIfNull(subscription.Strikes);
        if (subscription.Strikes.Count == 0)
        {
            throw new ArgumentException("At least one option strike is required.", nameof(subscription));
        }
        if (subscription.Rights == OptionRightSelection.None
            || (subscription.Rights & ~OptionRightSelection.Both) != 0)
        {
            throw new ArgumentException("Select Call, Put, or Both option rights.", nameof(subscription));
        }
        if (subscription.DataKinds == MarketDataKinds.None
            || (subscription.DataKinds & ~(MarketDataKinds.Quote
                                           | MarketDataKinds.Trade
                                           | MarketDataKinds.MboOrderUpdate)) != 0)
        {
            throw new ArgumentException("Option market-data kinds are invalid.", nameof(subscription));
        }
        var strikes = new HashSet<decimal>();
        foreach (var strike in subscription.Strikes)
        {
            if (((decimal.GetBits(strike)[3] >> 16) & 0x7f) > 9)
            {
                throw new ArgumentException(
                    $"Option strike {strike} has more than nine fractional decimal places.",
                    nameof(subscription));
            }
            if (!strikes.Add(strike))
            {
                throw new ArgumentException(
                    $"Duplicate option strike {strike}.",
                    nameof(subscription));
            }
        }
        ArgumentNullException.ThrowIfNull(subscription.ResolvedContracts);
        if (subscription.ResolvedContracts.Count == 0)
        {
            throw new ArgumentException("At least one resolved option contract is required.");
        }
        var keys = new HashSet<InstrumentKey>();
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        var selections = new OptionContractSelection[subscription.ResolvedContracts.Count];
        for (var index = 0; index < subscription.ResolvedContracts.Count; index++)
        {
            var contract = subscription.ResolvedContracts[index];
            ArgumentNullException.ThrowIfNull(contract);
            ArgumentException.ThrowIfNullOrWhiteSpace(contract.RawSymbol);
            if (!string.Equals(contract.Dataset, _options.Dataset, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Resolved contract '{contract.RawSymbol}' belongs to dataset "
                    + $"'{contract.Dataset}', not feed dataset '{_options.Dataset}'.",
                    nameof(subscription));
            }
            if (!string.Equals(contract.Underlying, subscription.Underlying, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Resolved contract '{contract.RawSymbol}' has underlying "
                    + $"'{contract.Underlying}', not '{subscription.Underlying}'.",
                    nameof(subscription));
            }
            if (contract.MaturityDate != subscription.MaturityDate)
            {
                throw new ArgumentException(
                    $"Resolved contract '{contract.RawSymbol}' has maturity "
                    + $"{contract.MaturityDate:yyyy-MM-dd}, not {subscription.MaturityDate:yyyy-MM-dd}.",
                    nameof(subscription));
            }
            if (contract.Right is not (OptionRightSelection.Call or OptionRightSelection.Put)
                || (subscription.Rights & contract.Right) == 0)
            {
                throw new ArgumentException(
                    $"Resolved contract '{contract.RawSymbol}' has an unselected option right.",
                    nameof(subscription));
            }
            if (!strikes.Contains(contract.StrikePrice))
            {
                throw new ArgumentException(
                    $"Resolved contract '{contract.RawSymbol}' has unselected strike "
                    + $"{contract.StrikePrice}.",
                    nameof(subscription));
            }
            if (contract.Instrument.PublisherId == 0 || contract.Instrument.InstrumentId == 0)
            {
                throw new ArgumentException(
                    $"Resolved contract '{contract.RawSymbol}' has an invalid provider instrument key.",
                    nameof(subscription));
            }
            if (Encoding.UTF8.GetByteCount(contract.RawSymbol) > ushort.MaxValue)
            {
                throw new ArgumentException(
                    "Option symbols cannot exceed 65,535 UTF-8 bytes.");
            }
            if (!keys.Add(contract.Instrument))
            {
                throw new ArgumentException($"Duplicate option instrument {contract.Instrument}.");
            }
            if (!symbols.Add(contract.RawSymbol))
            {
                throw new ArgumentException($"Duplicate option raw symbol '{contract.RawSymbol}'.");
            }
            selections[index] = new OptionContractSelection(
                contract.RawSymbol,
                contract.Instrument,
                contract.Right);
        }
        _inner.SubscribeOptionChain(
            selections,
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
