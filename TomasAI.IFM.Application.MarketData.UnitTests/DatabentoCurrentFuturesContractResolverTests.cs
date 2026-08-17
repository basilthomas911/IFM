using FluentAssertions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatabentoCurrentFuturesContractResolverTests
{
    [Fact]
    public async Task ResolvesNearestEligibleMaturityAfterValueDatePlusOneDay()
    {
        var factory = new FakeFeedFactory([
            Detail("ESU6", "ES", new DateOnly(2026, 9, 18)),
            Detail("ESZ6", "ES", new DateOnly(2026, 12, 18))]);
        var resolver = new DatabentoCurrentFuturesContractResolver(
            factory, Options());

        var result = await resolver.ResolveAsync("es", new DateOnly(2026, 8, 14));

        result.Contract.ContractId.Should().Be("ES20260918");
        result.Contract.LocalSymbol.Should().Be("ESU6");
        result.Contract.CurrentlyTraded.Should().BeTrue();
        result.NextRolloverDate.Should().Be(new DateOnly(2026, 9, 18));
        factory.LastDataset.Should().Be("GLBX.MDP3");
        factory.LastTicker.Should().Be("ES.FUT");
    }

    [Fact]
    public async Task UsesCboeFuturesDatasetForVx()
    {
        var factory = new FakeFeedFactory([
            Detail("VX/U6", "VX", new DateOnly(2026, 9, 16), "CFE")]);
        var resolver = new DatabentoCurrentFuturesContractResolver(factory, Options());

        await resolver.ResolveAsync("VX", new DateOnly(2026, 8, 14));

        factory.LastDataset.Should().Be("XCBF.PITCH");
        factory.LastTicker.Should().Be("VX.FUT");
    }

    [Fact]
    public async Task UsesSettlementCurrencyWhenDefinitionCurrencyIsMissing()
    {
        var factory = new FakeFeedFactory([
            Detail(
                "VX/Q6",
                "VX",
                new DateOnly(2026, 8, 19),
                "XCBF",
                currency: string.Empty,
                settlementCurrency: "usd")]);
        var resolver = new DatabentoCurrentFuturesContractResolver(factory, Options());

        var result = await resolver.ResolveAsync("VX", new DateOnly(2026, 8, 17));

        result.Contract.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task RejectsDefinitionWhenBothCurrenciesAreMissing()
    {
        var factory = new FakeFeedFactory([
            Detail(
                "ABCQ6",
                "ABC",
                new DateOnly(2026, 8, 19),
                "XCBF",
                currency: string.Empty,
                settlementCurrency: " ")]);
        var resolver = new DatabentoCurrentFuturesContractResolver(
            factory,
            Options() with
            {
                FuturesContractCurrencyFallbacks =
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            });

        var act = () => resolver.ResolveAsync("ABC", new DateOnly(2026, 8, 17));

        await act.Should().ThrowAsync<MarketDataContractMappingException>()
            .WithMessage("*both the contract currency and settlement currency are missing*");
    }

    [Fact]
    public async Task UsesConfiguredCurrencyWhenVxDefinitionCurrenciesAreMissing()
    {
        var factory = new FakeFeedFactory([
            Detail(
                "VX/Q6",
                "VX",
                new DateOnly(2026, 8, 19),
                "XCBF",
                currency: string.Empty,
                settlementCurrency: string.Empty)]);
        var resolver = new DatabentoCurrentFuturesContractResolver(factory, Options());

        var result = await resolver.ResolveAsync("VX", new DateOnly(2026, 8, 17));

        result.Contract.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task ThrowsTypedFailureWhenNoEligibleContractExists()
    {
        var factory = new FakeFeedFactory([
            Detail("ESQ6", "ES", new DateOnly(2026, 8, 14))]);
        var resolver = new DatabentoCurrentFuturesContractResolver(factory, Options());

        var act = () => resolver.ResolveAsync("ES", new DateOnly(2026, 8, 14));

        await act.Should().ThrowAsync<CurrentlyTradedFuturesContractNotFoundException>();
    }

    private static DatabentoMarketDataRuntimeOptions Options() => new()
    {
        FeedOptions = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.SyntheticCi, "GLBX.MDP3"),
        Contracts = []
    };

    private static ContractDetail Detail(
        string rawSymbol,
        string ticker,
        DateOnly maturity,
        string exchange = "CME",
        string currency = "USD",
        string settlementCurrency = "USD") => new()
    {
        Dataset = "test",
        RawSymbol = rawSymbol,
        Ticker = ticker,
        Underlying = ticker,
        Instrument = new InstrumentKey(1, (uint)rawSymbol.GetHashCode()),
        ContractKind = ContractKind.Future,
        MaturityDate = maturity,
        ContractMultiplier = 50,
        Currency = currency,
        SettlementCurrency = settlementCurrency,
        Exchange = exchange,
        SecurityType = "FUT",
        Cfi = string.Empty,
        UnitOfMeasure = "USD"
    };

    private sealed class FakeFeedFactory(IReadOnlyList<ContractDetail> details)
        : IDatabentoFeedFactory
    {
        internal string? LastDataset { get; private set; }
        internal string? LastTicker { get; set; }

        public IDatabentoMarketDataQueries CreateMarketDataQueries(DatabentoFeedOptions options)
        {
            LastDataset = options.Dataset;
            return new FakeQueries(details, this);
        }

        public IDatabentoTickerFeed CreateTickerFeed(DatabentoFeedOptions options) =>
            throw new NotSupportedException();
        public IDatabentoOptionChainFeed CreateOptionChainFeed(DatabentoFeedOptions options) =>
            throw new NotSupportedException();
        public IDatabentoLatestPriceClient CreateLatestPriceClient(DatabentoFeedOptions options) =>
            throw new NotSupportedException();
    }

    private sealed class FakeQueries(
        IReadOnlyList<ContractDetail> details,
        FakeFeedFactory owner) : IDatabentoMarketDataQueries
    {
        public IReadOnlyList<ContractDetail> GetContractDetails(
            string ticker, TimeSpan? timeout = null)
        {
            owner.LastTicker = ticker;
            var root = ticker.Split('.')[0];
            return details.Where(detail => detail.Ticker == root).ToArray();
        }

        public OptionChainDefinitions GetChainDefinitions(
            OptionChainDefinitionRequest request, TimeSpan? timeout = null) =>
            throw new NotSupportedException();
        public uint ContractIdToInstrumentId(string contractId, TimeSpan? timeout = null) =>
            throw new NotSupportedException();
        public string InstrumentIdToContractId(uint instrumentId, TimeSpan? timeout = null) =>
            throw new NotSupportedException();
        public ContractDetail? GetContractDetail(string contractName, TimeSpan? timeout = null) =>
            throw new NotSupportedException();
        public IReadOnlyList<ContractDetail?> GetContractDetails(
            string[] contractNames, TimeSpan? timeout = null) =>
            throw new NotSupportedException();
    }
}
