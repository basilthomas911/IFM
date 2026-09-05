using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.TradeStrategyFamilies;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.UnitTests.Queries;

public sealed class TradeStrategyFamilyCreationTests
{
    static CreateTradeStrategyFamilyRequest Request() => new() { OperationId = Guid.NewGuid(), Family = TradeStrategyFamilyType.FuturesOption, Strategy = TradeStrategyType.VerticalSpread, TimeFrame = TimeFrameType.Weekly, TradeStrategySymbolId = 101, Description = "Weekly ES spread" };
    static TradeStrategySymbolReadModel Product() => new() { Id = 101, Symbol = "ES", Currency = "USD", Exchange = "XCME", Description = "ES futures options" };

    [Fact]
    public async Task Creation_uses_provider_metadata_and_server_audit_not_client_invented_fields()
    {
        var api = Substitute.For<IMarketDataApi>(); var store = Substitute.For<ITradeStrategyFamilyCatalogStore>(); var request = Request();
        api.GetTradeStrategySymbolsAsync(request.Family, Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([Product()]));
        store.CreateAsync(request, Arg.Any<TradeStrategyFamilyReadModel>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<TradeStrategyFamilyReadModel>() with { TradeStrategyFamilyId = 201, DefinitionVersion = 1, State = TradeStrategyFamilyState.Active });
        var result = await new TradeStrategyFamilyCreationService(api, store, TimeProvider.System).CreateAsync(request, "operator");
        result.TradeStrategySymbolId.Should().Be(101); result.Symbol.Should().Be("ES"); result.Exchange.Should().Be("XCME"); result.Currency.Should().Be("USD");
        result.SystemKey.Should().Be("FuturesOption-VerticalSpread"); result.CreatedBy.Should().Be("operator"); result.Validate().Should().BeEmpty();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("currency")]
    [InlineData("exchange")]
    [InlineData("symbol")]
    [InlineData("failed")]
    public async Task Unavailable_or_incomplete_product_prevents_writes(string kind)
    {
        var api = Substitute.For<IMarketDataApi>(); var store = Substitute.For<ITradeStrategyFamilyCatalogStore>(); var request = Request();
        var product = kind switch { "missing" => Product() with { Id = 999 }, "currency" => Product() with { Currency = "" }, "exchange" => Product() with { Exchange = "" }, "symbol" => Product() with { Symbol = " " }, _ => Product() };
        api.GetTradeStrategySymbolsAsync(request.Family, Arg.Any<CancellationToken>()).Returns(kind == "failed" ?
            new ServiceFailed<TradeStrategySymbolReadModel[]>(503, "offline") : new ServiceOk<TradeStrategySymbolReadModel[]>([product]));
        var call = () => new TradeStrategyFamilyCreationService(api, store, TimeProvider.System).CreateAsync(request, "test");
        await call.Should().ThrowAsync<Exception>(); store.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData(TradeStrategyFamilyType.Futures, TradeStrategyType.IronCondor)]
    [InlineData(TradeStrategyFamilyType.FuturesOption, TradeStrategyType.Futures)]
    [InlineData(TradeStrategyFamilyType.Equity, TradeStrategyType.VerticalSpread)]
    public void Unsupported_family_strategy_pairs_are_rejected(TradeStrategyFamilyType family, TradeStrategyType strategy) =>
        (Request() with { Family = family, Strategy = strategy }).Validate().Should().NotBeEmpty();
}
