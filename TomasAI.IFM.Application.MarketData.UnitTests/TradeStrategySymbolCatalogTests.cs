using FluentAssertions;
using MessagePack;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.TradeStrategySymbols;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class TradeStrategySymbolCatalogTests
{
    static readonly TradeStrategyProduct Es = new(TradeStrategyFamilyType.Futures, "ES", "USD", "XCME");
    sealed class Clock : TimeProvider { public DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }

    [Fact]
    public void Read_model_has_the_requested_five_keys_and_round_trips()
    {
        var row = Es.WithId(101);
        MessagePackSerializer.Deserialize<TradeStrategySymbolReadModel>(MessagePackSerializer.Serialize(row)).Should().Be(row);
        typeof(TradeStrategySymbolReadModel).GetProperties().Select(x => (x.Name, Key: x.GetCustomAttributes(typeof(KeyAttribute), false).Cast<KeyAttribute>().Single().IntKey))
            .OrderBy(x => x.Key).Select(x => x.Name).Should().Equal("Id", "Symbol", "Currency", "Exchange", "Description");
        row.Description.Should().Be("ES futures");
        (Es with { Family = TradeStrategyFamilyType.FuturesOption }).Description.Should().Be("ES futures options");
    }

    [Theory]
    [InlineData(TradeStrategyFamilyType.Unknown)]
    [InlineData(TradeStrategyFamilyType.Equity)]
    [InlineData(TradeStrategyFamilyType.EquityOptions)]
    [InlineData(TradeStrategyFamilyType.FixedIncome)]
    [InlineData(TradeStrategyFamilyType.FixedIncomeOptions)]
    [InlineData((TradeStrategyFamilyType)999)]
    public async Task Unsupported_families_fail_without_provider_or_store_calls(TradeStrategyFamilyType family)
    {
        var source = Substitute.For<ITradeStrategySymbolSource>(); var store = Substitute.For<ITradeStrategySymbolStore>();
        var service = new TradeStrategySymbolCatalog(source, store, new Clock());
        var result = await service.GetAsync(family);
        result.Success.Should().BeFalse(); result.ErrorMessage.Should().Contain("not supported");
        source.ReceivedCalls().Should().BeEmpty(); store.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData("symbol")]
    [InlineData("currency")]
    [InlineData("exchange")]
    public async Task Missing_metadata_fails_entire_result_before_allocating_any_ids(string field)
    {
        var bad = field switch { "symbol" => Es with { Symbol = " " }, "currency" => Es with { Currency = "" }, _ => Es with { Exchange = "" } };
        var source = Substitute.For<ITradeStrategySymbolSource>(); var store = Substitute.For<ITradeStrategySymbolStore>();
        source.DiscoverAsync(Es.Family, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<TradeStrategyProduct>>([Es, bad]));
        var result = await new TradeStrategySymbolCatalog(source, store, new Clock()).GetAsync(Es.Family);
        result.Success.Should().BeFalse(); result.ErrorMessage.Should().ContainEquivalentOf(field);
        store.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Cache_is_bounded_by_time_and_returns_defensive_arrays_and_stable_ids_after_refresh()
    {
        var source = Substitute.For<ITradeStrategySymbolSource>(); var store = Substitute.For<ITradeStrategySymbolStore>(); var clock = new Clock();
        source.DiscoverAsync(Es.Family, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<TradeStrategyProduct>>([Es, Es]));
        store.GetOrCreateAsync(Es, Arg.Any<CancellationToken>()).Returns(Es.WithId(101));
        var service = new TradeStrategySymbolCatalog(source, store, clock);
        var first = await service.GetAsync(Es.Family); first.Value!.Should().ContainSingle(); first.Value[0] = Es.WithId(999);
        (await service.GetAsync(Es.Family)).Value![0].Id.Should().Be(101);
        clock.Now += TimeSpan.FromMinutes(6);
        (await service.GetAsync(Es.Family)).Value![0].Id.Should().Be(101);
        await source.Received(2).DiscoverAsync(Es.Family, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Failed_refresh_does_not_serve_expired_cache_or_partial_results()
    {
        var source = Substitute.For<ITradeStrategySymbolSource>(); var store = Substitute.For<ITradeStrategySymbolStore>(); var clock = new Clock();
        source.DiscoverAsync(Es.Family, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<TradeStrategyProduct>>([Es]), Task.FromException<IReadOnlyList<TradeStrategyProduct>>(new InvalidOperationException("provider offline")));
        store.GetOrCreateAsync(Es, Arg.Any<CancellationToken>()).Returns(Es.WithId(101));
        var service = new TradeStrategySymbolCatalog(source, store, clock);
        (await service.GetAsync(Es.Family)).Success.Should().BeTrue(); clock.Now += TimeSpan.FromMinutes(6);
        var result = await service.GetAsync(Es.Family); result.Success.Should().BeFalse(); result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Provider_and_catalog_are_integrated_without_starting_live_streams()
    {
        var factory = Substitute.For<IDatabentoFeedFactory>(); var queries = Substitute.For<IDatabentoMarketDataQueries>();
        factory.CreateMarketDataQueries(Arg.Any<DatabentoFeedOptions>()).Returns(queries);
        queries.GetDatasetDefinitions(Arg.Any<TimeSpan?>()).Returns([Future(), Future() with { RawSymbol = "ESZ6", Instrument = new(1, 2) },
            Future() with { RawSymbol = "EW1-call", Ticker = "EW1", ContractKind = ContractKind.CallOption, UnderlyingInstrumentId = 1 }]);
        var source = new DatabentoTradeStrategySymbolSource(factory, Options(), new Clock());
        var result = await source.DiscoverAsync(TradeStrategyFamilyType.FuturesOption, CancellationToken.None);
        result.Should().ContainSingle().Which.Should().Be(Es with { Family = TradeStrategyFamilyType.FuturesOption });
        factory.ReceivedCalls().Should().OnlyContain(x => x.GetMethodInfo().Name == nameof(IDatabentoFeedFactory.CreateMarketDataQueries));
    }

    [Fact]
    public async Task Provider_does_not_substitute_settlement_currency_for_missing_price_currency()
    {
        var factory = Substitute.For<IDatabentoFeedFactory>(); var queries = Substitute.For<IDatabentoMarketDataQueries>();
        factory.CreateMarketDataQueries(Arg.Any<DatabentoFeedOptions>()).Returns(queries);
        queries.GetDatasetDefinitions(Arg.Any<TimeSpan?>()).Returns([Future() with { Currency = "", SettlementCurrency = "USD" }]);
        var call = () => new DatabentoTradeStrategySymbolSource(factory, Options(), new Clock()).DiscoverAsync(Es.Family, CancellationToken.None);
        await call.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No eligible*Currency*");
    }

    [Fact]
    public async Task Cancellation_does_not_query_or_write()
    {
        var source = Substitute.For<ITradeStrategySymbolSource>(); var store = Substitute.For<ITradeStrategySymbolStore>();
        var call = () => new TradeStrategySymbolCatalog(source, store, new Clock()).GetAsync(Es.Family, new CancellationToken(true));
        await call.Should().ThrowAsync<OperationCanceledException>(); source.ReceivedCalls().Should().BeEmpty(); store.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Futures_without_options_are_not_included_in_the_option_product_catalog()
    {
        var factory = Substitute.For<IDatabentoFeedFactory>(); var queries = Substitute.For<IDatabentoMarketDataQueries>();
        factory.CreateMarketDataQueries(Arg.Any<DatabentoFeedOptions>()).Returns(queries);
        queries.GetDatasetDefinitions(Arg.Any<TimeSpan?>()).Returns([Future(),
            Future() with { RawSymbol = "EW1-call", ContractKind = ContractKind.CallOption, UnderlyingInstrumentId = 1 },
            Future() with { Ticker = "NQ", RawSymbol = "NQU6", Instrument = new(1, 2) }]);
        var source = new DatabentoTradeStrategySymbolSource(factory, Options(), new Clock());
        (await source.DiscoverAsync(TradeStrategyFamilyType.FuturesOption, CancellationToken.None)).Select(x => x.Symbol).Should().Equal("ES");
        (await source.DiscoverAsync(TradeStrategyFamilyType.Futures, CancellationToken.None)).Select(x => x.Symbol).Should().BeEquivalentTo("ES", "NQ");
        queries.Received(1).GetDatasetDefinitions(Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Wrong_underlying_id_is_not_overridden_by_a_matching_raw_symbol()
    {
        var factory = Substitute.For<IDatabentoFeedFactory>(); var queries = Substitute.For<IDatabentoMarketDataQueries>();
        factory.CreateMarketDataQueries(Arg.Any<DatabentoFeedOptions>()).Returns(queries);
        queries.GetDatasetDefinitions(Arg.Any<TimeSpan?>()).Returns([Future(),
            Future() with { ContractKind = ContractKind.CallOption, UnderlyingInstrumentId = 999, Underlying = "ESU6" }]);
        var source = new DatabentoTradeStrategySymbolSource(factory, Options(), new Clock());
        (await source.DiscoverAsync(TradeStrategyFamilyType.FuturesOption, CancellationToken.None)).Should().BeEmpty();
    }
    static DatabentoMarketDataRuntimeOptions Options() => new() { Contracts = [], FeedOptions = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development, "GLBX.MDP3") with { DataSource = FeedDataSourceMode.DatabentoLive }, TradeStrategySymbolDatasets = ["GLBX.MDP3"] };
    static ContractDetail Future() => new() { Dataset = "GLBX.MDP3", RawSymbol = "ESU6", Ticker = "ES", Underlying = "", Instrument = new(1, 1), ContractKind = ContractKind.Future, MaturityDate = new(2026, 9, 18), Currency = "USD", SettlementCurrency = "", Exchange = "XCME", SecurityType = "FUT", Cfi = "", UnitOfMeasure = "" };
}
