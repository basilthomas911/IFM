using System.Text;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal sealed class SyntheticOptionChainFeed : IDatabentoOptionChainFeed
{
    private readonly DatabentoFeedOptions _options;
    private readonly SyntheticTickerFeed _inner;
    private InstrumentKey? _firstInstrument;
    private bool _subscribed;

    /// <summary>Initializes a new SyntheticOptionChainFeed instance.</summary>
    /// <param name="options">The configuration governing provider or feed operation.</param>
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

    /// <summary>Registers the requested instruments before feed processing begins.</summary>
    /// <param name="subscription">The option-chain subscription identifying the instruments to stream.</param>
    /// <param name="timeout">The maximum time allowed for the operation.</param>
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
                                           | MarketDataKinds.MboOrderUpdate
                                           | MarketDataKinds.Statistics
                                           | MarketDataKinds.SessionVolume)) != 0)
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
        var instrumentIds = new HashSet<uint>();
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
            if (contract.Instrument.InstrumentId == 0)
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
            if (!instrumentIds.Add(contract.Instrument.InstrumentId))
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

    /// <summary>Starts feed processing or monitoring.</summary>
    /// <param name="timeout">The maximum time allowed for the operation.</param>
    /// <param name="startConsumer">The callback that starts draining the feed after startup.</param>
    public void Start(TimeSpan timeout, Action<TimeSpan> startConsumer)
    {
        ArgumentNullException.ThrowIfNull(startConsumer);
        _inner.Start(timeout, remaining =>
        {
            _firstInstrument = _inner.GetInstruments()[0].Instrument;
            startConsumer(remaining);
        });
    }

    /// <summary>Stops feed processing or monitoring within the specified timeout.</summary>
    /// <param name="timeout">The maximum time allowed for the operation.</param>
    public void Stop(TimeSpan timeout) => _inner.Stop(timeout);

    /// <summary>Captures the feed&apos;s current health and processing counters.</summary>
    /// <returns>The health result.</returns>
    public FeedHealthSnapshot GetHealth() => _inner.GetHealth();

    /// <summary>Releases the resources owned by this instance.</summary>
    public void Dispose() => _inner.Dispose();
}
