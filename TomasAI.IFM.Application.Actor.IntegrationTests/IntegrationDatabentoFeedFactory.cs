using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Application.Actor.IntegrationTests;

/// <summary>
/// Deterministic provider boundary used by actor integration tests. The application
/// market-data API, catalog, aggregation service, actors, and transports remain real.
/// </summary>
internal sealed class IntegrationDatabentoFeedFactory : IDatabentoFeedFactory
{
    private static readonly IReadOnlyList<ContractDetail> Details =
    [
        Future("ESZ5", 101),
        VixFuture("VXZ6", 107),
        Option("EW1K6 C5000", 102, ContractKind.CallOption, 5000),
        Option("ESZ5 P5400", 103, ContractKind.PutOption, 5400),
        Option("ESZ5 P5300", 104, ContractKind.PutOption, 5300),
        Option("ESZ5 C5500", 105, ContractKind.CallOption, 5500),
        Option("ESZ5 C5600", 106, ContractKind.CallOption, 5600)
    ];

    private static readonly IReadOnlyDictionary<string, ContractDetail> BySymbol =
        Details.ToDictionary(detail => detail.RawSymbol, StringComparer.Ordinal);

    public IDatabentoTickerFeed CreateTickerFeed(DatabentoFeedOptions options) =>
        new IntegrationTickerFeed(BySymbol);

    public IDatabentoOptionChainFeed CreateOptionChainFeed(DatabentoFeedOptions options) =>
        new IntegrationOptionChainFeed();

    public IDatabentoMarketDataQueries CreateMarketDataQueries(DatabentoFeedOptions options) =>
        new IntegrationQueries(BySymbol);

    public IDatabentoLatestPriceClient CreateLatestPriceClient(DatabentoFeedOptions options) =>
        throw new NotSupportedException("Integration tests use the application last-price readers.");

    private static ContractDetail Future(string rawSymbol, uint instrumentId) => new()
    {
        Dataset = "GLBX.MDP3",
        RawSymbol = rawSymbol,
        Ticker = "ES",
        Underlying = rawSymbol,
        Instrument = new InstrumentKey(1, instrumentId),
        ContractKind = ContractKind.Future,
        MaturityDate = new DateOnly(2025, 12, 19),
        ContractMultiplier = 50,
        Currency = "USD",
        SettlementCurrency = "USD",
        Exchange = "CME",
        SecurityType = "FUT",
        Cfi = string.Empty,
        UnitOfMeasure = "USD"
    };

    private static ContractDetail VixFuture(string rawSymbol, uint instrumentId) => new()
    {
        Dataset = "GLBX.MDP3",
        RawSymbol = rawSymbol,
        Ticker = "VX",
        Underlying = rawSymbol,
        Instrument = new InstrumentKey(1, instrumentId),
        ContractKind = ContractKind.Future,
        MaturityDate = new DateOnly(2026, 12, 16),
        ContractMultiplier = 1000,
        Currency = "USD",
        SettlementCurrency = "USD",
        Exchange = "CFE",
        SecurityType = "FUT",
        Cfi = string.Empty,
        UnitOfMeasure = "USD"
    };

    private static ContractDetail Option(
        string rawSymbol,
        uint instrumentId,
        ContractKind kind,
        long strike) => new()
    {
        Dataset = "GLBX.MDP3",
        RawSymbol = rawSymbol,
        Ticker = "ES",
        Underlying = "ESZ5",
        Instrument = new InstrumentKey(1, instrumentId),
        ContractKind = kind,
        StrikePrice = checked(strike * 1_000_000_000L),
        MaturityDate = new DateOnly(2025, 12, 19),
        ContractMultiplier = 50,
        Currency = "USD",
        SettlementCurrency = "USD",
        Exchange = "CME",
        SecurityType = "FOP",
        Cfi = string.Empty,
        UnitOfMeasure = "USD"
    };

    private sealed class IntegrationQueries(
        IReadOnlyDictionary<string, ContractDetail> details) : IDatabentoMarketDataQueries
    {
        public OptionChainDefinitions GetChainDefinitions(
            OptionChainDefinitionRequest request,
            TimeSpan? timeout = null)
        {
            var contracts = details.Values
                .Where(detail => detail.ContractKind != ContractKind.Future
                    && detail.Underlying == request.Underlying
                    && detail.MaturityDate == request.MaturityDate)
                .Select(detail => new OptionContractDefinition
                {
                    Dataset = detail.Dataset,
                    RawSymbol = detail.RawSymbol,
                    Ticker = detail.Ticker,
                    Underlying = detail.Underlying,
                    Instrument = detail.Instrument,
                    Right = detail.ContractKind == ContractKind.CallOption
                        ? OptionRightSelection.Call
                        : OptionRightSelection.Put,
                    StrikePrice = detail.StrikePrice!.Value / 1_000_000_000m,
                    MaturityDate = detail.MaturityDate!.Value,
                    ContractMultiplier = detail.ContractMultiplier
                })
                .ToArray();
            return new OptionChainDefinitions
            {
                Dataset = request.Dataset,
                Underlying = request.Underlying,
                MaturityDate = request.MaturityDate,
                UniversePolicy = request.UniversePolicy,
                Rights = request.Rights,
                Contracts = contracts
            };
        }

        public uint ContractIdToInstrumentId(string contractId, TimeSpan? timeout = null) =>
            details[contractId].Instrument.InstrumentId;

        public string InstrumentIdToContractId(uint instrumentId, TimeSpan? timeout = null) =>
            details.Values.Single(detail => detail.Instrument.InstrumentId == instrumentId).RawSymbol;

        public ContractDetail? GetContractDetail(string contractName, TimeSpan? timeout = null) =>
            details.GetValueOrDefault(contractName);

        public IReadOnlyList<ContractDetail> GetContractDetails(string ticker, TimeSpan? timeout = null) =>
            details.Values.Where(detail => detail.Ticker == ticker).ToArray();

        public IReadOnlyList<ContractDetail?> GetContractDetails(
            string[] contractNames,
            TimeSpan? timeout = null) =>
            contractNames.Select(details.GetValueOrDefault).ToArray();
    }

    private sealed class IntegrationTickerFeed(
        IReadOnlyDictionary<string, ContractDetail> details) : IDatabentoTickerFeed
    {
        private readonly BlockingMultiplexedReader _reader = new();
        private TickerSubscription[] _subscriptions = [];

        public void Subscribe(ReadOnlySpan<TickerSubscription> subscriptions, TimeSpan timeout) =>
            _subscriptions = subscriptions.ToArray();

        public void Start(TimeSpan timeout) { }

        public void Stop(TimeSpan timeout) => _reader.Complete();

        public ISynchronousBatchReader<MarketDataBatch64> GetReader(InstrumentKey instrument) =>
            throw new NotSupportedException();

        public IMultiplexedTickerBatchReader GetMultiplexedReader() => _reader;

        public IReadOnlyList<TickerInstrumentRegistration> GetInstruments() =>
            _subscriptions.Select((subscription, index) =>
            {
                var detail = details[subscription.Symbol];
                return new TickerInstrumentRegistration(
                    subscription.Symbol,
                    detail.RawSymbol,
                    new InstrumentKey(1, checked((uint)index + 1)));
            }).ToArray();

        public FeedHealthSnapshot GetHealth() => HealthyFeed();

        public void Dispose() => _reader.Complete();
    }

    private sealed class IntegrationOptionChainFeed : IDatabentoOptionChainFeed
    {
        private readonly BlockingBatchReader _reader = new();
        public ISynchronousBatchReader<MarketDataBatch64> Reader => _reader;
        public void Subscribe(OptionChainSubscription subscription, TimeSpan timeout) { }
        public void Start(TimeSpan timeout) { }
        public void Stop(TimeSpan timeout) => _reader.Complete();
        public FeedHealthSnapshot GetHealth() => HealthyFeed();
        public void Dispose() => _reader.Complete();
    }

    private sealed class BlockingMultiplexedReader : IMultiplexedTickerBatchReader
    {
        private readonly ManualResetEventSlim _completed = new(false);
        public bool IsCompleted => _completed.IsSet;
        public bool TryRead(out InstrumentBatch64 batch) { batch = default; return false; }
        public bool TryRead(TimeSpan timeout, out InstrumentBatch64 batch)
        {
            _completed.Wait(timeout);
            batch = default;
            return false;
        }
        public InstrumentBatch64 Read(TimeSpan timeout)
        {
            if (_completed.Wait(timeout)) throw new EndOfStreamException();
            throw new TimeoutException();
        }
        public void Complete() => _completed.Set();
        public void Dispose() => Complete();
    }

    private sealed class BlockingBatchReader : ISynchronousBatchReader<MarketDataBatch64>
    {
        private readonly ManualResetEventSlim _completed = new(false);
        public bool IsCompleted => _completed.IsSet;
        public bool TryRead(out MarketDataBatch64? batch) { batch = null; return false; }
        public bool TryRead(TimeSpan timeout, out MarketDataBatch64? batch)
        {
            _completed.Wait(timeout);
            batch = null;
            return false;
        }
        public MarketDataBatch64 Read(TimeSpan timeout)
        {
            if (_completed.Wait(timeout)) throw new EndOfStreamException();
            throw new TimeoutException();
        }
        public void Complete() => _completed.Set();
    }

    private static FeedHealthSnapshot HealthyFeed() => new(
        FeedState.Running,
        DatabentoFeedStatus.Ok,
        1024,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        null)
    {
        TransportReady = true,
        TradingReady = true
    };
}
