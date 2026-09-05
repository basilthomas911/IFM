using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class TradeStrategyDatasetDiscoveryTests
{
    sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    static ContractDetail Future(string symbol, uint id) => new()
    {
        Dataset = "GLBX.MDP3", RawSymbol = symbol + "Z6", Ticker = symbol, Underlying = "", Instrument = new(1, id),
        ContractKind = ContractKind.Future, MaturityDate = new(2026, 12, 18), Currency = "USD", Exchange = "XCME",
        SettlementCurrency = "", SecurityType = "FUT", Cfi = "", UnitOfMeasure = ""
    };
    static ContractDetail Option(ContractDetail underlying) => underlying with
    {
        RawSymbol = "different-root-" + underlying.RawSymbol, Ticker = "different-root", ContractKind = ContractKind.CallOption,
        UnderlyingInstrumentId = underlying.Instrument.InstrumentId, Instrument = new(1, underlying.Instrument.InstrumentId + 10000)
    };
    static DatabentoMarketDataRuntimeOptions Settings() => new()
    {
        Contracts = [], FeedOptions = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development, "GLBX.MDP3")
            with { DataSource = FeedDataSourceMode.DatabentoLive }
    };

    [Fact]
    public async Task Discovers_more_than_100_non_seed_products_and_maps_option_roots_to_futures()
    {
        var factory = Substitute.For<IDatabentoFeedFactory>(); var query = Substitute.For<IDatabentoMarketDataQueries>();
        factory.CreateMarketDataQueries(Arg.Any<DatabentoFeedOptions>()).Returns(query);
        var futures = new[] { Future("ES", 1), Future("NQ", 2), Future("CL", 3) }
            .Concat(Enumerable.Range(4, 150).Select(x => Future("PRODUCT" + x, (uint)x))).ToArray();
        query.GetDatasetDefinitions(Arg.Any<TimeSpan?>()).Returns(futures.Concat(futures.Select(Option)).ToArray());
        var source = new DatabentoTradeStrategySymbolSource(factory, Settings(), new Clock());
        var products = await source.DiscoverAsync(TradeStrategyFamilyType.FuturesOption, CancellationToken.None);
        products.Should().HaveCount(153); products.Select(x => x.Symbol).Should().Contain(["ES", "NQ", "CL", "PRODUCT153"]);
        products.Should().OnlyContain(x => x.Currency == "USD" && x.Exchange == "XCME");
        (await source.DiscoverAsync(TradeStrategyFamilyType.Futures, CancellationToken.None)).Should().HaveCount(153);
        query.Received(1).GetDatasetDefinitions(Arg.Any<TimeSpan?>());
        query.ReceivedCalls().Should().OnlyContain(x => x.GetMethodInfo().Name == nameof(IDatabentoMarketDataQueries.GetDatasetDefinitions));
        factory.ReceivedCalls().Should().OnlyContain(x => x.GetMethodInfo().Name == nameof(IDatabentoFeedFactory.CreateMarketDataQueries));
    }

    [Fact]
    public async Task Refresh_discovers_new_products_and_expired_cache_is_not_returned_on_failure()
    {
        var factory = Substitute.For<IDatabentoFeedFactory>(); var query = Substitute.For<IDatabentoMarketDataQueries>(); var clock = new Clock();
        factory.CreateMarketDataQueries(Arg.Any<DatabentoFeedOptions>()).Returns(query);
        query.GetDatasetDefinitions(Arg.Any<TimeSpan?>()).Returns([Future("ES", 1)]);
        var source = new DatabentoTradeStrategySymbolSource(factory, Settings(), clock);
        (await source.DiscoverAsync(TradeStrategyFamilyType.Futures, CancellationToken.None)).Should().ContainSingle();
        clock.Now += TimeSpan.FromMinutes(6);
        query.GetDatasetDefinitions(Arg.Any<TimeSpan?>()).Returns([Future("ES", 1), Future("NQ", 2)]);
        (await source.DiscoverAsync(TradeStrategyFamilyType.Futures, CancellationToken.None)).Select(x => x.Symbol).Should().BeEquivalentTo("ES", "NQ");
        clock.Now += TimeSpan.FromMinutes(6);
        query.GetDatasetDefinitions(Arg.Any<TimeSpan?>()).Returns(_ => throw new InvalidOperationException("provider unavailable"));
        await FluentActions.Awaiting(() => source.DiscoverAsync(TradeStrategyFamilyType.Futures, CancellationToken.None)).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Filters_expired_incomplete_and_non_outright_options_without_hiding_valid_products()
    {
        var factory = Substitute.For<IDatabentoFeedFactory>(); var query = Substitute.For<IDatabentoMarketDataQueries>();
        factory.CreateMarketDataQueries(Arg.Any<DatabentoFeedOptions>()).Returns(query);
        var es = Future("ES", 1); var expired = Future("OLD", 2) with { MaturityDate = new(2026, 8, 1) };
        query.GetDatasetDefinitions(Arg.Any<TimeSpan?>()).Returns([es, Option(es), expired, Option(expired),
            Future("BAD", 3) with { Currency = "" }, Option(es) with { UnderlyingInstrumentId = 999 }, Option(es) with { Exchange = "" }]);
        var source = new DatabentoTradeStrategySymbolSource(factory, Settings(), new Clock());
        (await source.DiscoverAsync(TradeStrategyFamilyType.FuturesOption, CancellationToken.None)).Select(x => x.Symbol).Should().Equal("ES");
        (await source.DiscoverAsync(TradeStrategyFamilyType.Futures, CancellationToken.None)).Select(x => x.Symbol).Should().Equal("ES");
    }

    [Fact]
    public async Task Underlying_identity_is_publisher_qualified_and_name_fallback_requires_an_absent_id()
    {
        var factory = Substitute.For<IDatabentoFeedFactory>(); var query = Substitute.For<IDatabentoMarketDataQueries>();
        factory.CreateMarketDataQueries(Arg.Any<DatabentoFeedOptions>()).Returns(query);
        var es = Future("ES", 1);
        var nq = Future("NQ", 1) with { Instrument = new(2, 1) };
        var cl = Future("CL", 3);
        query.GetDatasetDefinitions(Arg.Any<TimeSpan?>()).Returns([es, nq, cl,
            Option(nq) with { Instrument = new(2, 10001) },
            Option(cl) with { UnderlyingInstrumentId = 0, Underlying = cl.RawSymbol },
            Option(es) with { UnderlyingInstrumentId = 999, Underlying = es.RawSymbol }]);
        var source = new DatabentoTradeStrategySymbolSource(factory, Settings(), new Clock());
        (await source.DiscoverAsync(TradeStrategyFamilyType.FuturesOption, CancellationToken.None))
            .Select(x => x.Symbol).Should().BeEquivalentTo("NQ", "CL");
    }

    [Fact]
    public async Task Dataset_failure_never_returns_a_partially_discovered_universe()
    {
        var factory = Substitute.For<IDatabentoFeedFactory>(); var query = Substitute.For<IDatabentoMarketDataQueries>();
        factory.CreateMarketDataQueries(Arg.Any<DatabentoFeedOptions>()).Returns(query);
        query.GetDatasetDefinitions(Arg.Any<TimeSpan?>()).Returns(_ => new[] { Future("ES", 1) }, _ => throw new InvalidOperationException("dataset unavailable"));
        var source = new DatabentoTradeStrategySymbolSource(factory, Settings() with { TradeStrategySymbolDatasets = ["GLBX.MDP3", "XOTHER"] }, new Clock());
        await FluentActions.Awaiting(() => source.DiscoverAsync(TradeStrategyFamilyType.Futures, CancellationToken.None)).Should().ThrowAsync<InvalidOperationException>();
    }
}
