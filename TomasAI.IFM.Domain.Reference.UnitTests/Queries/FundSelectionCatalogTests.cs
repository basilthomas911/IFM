using FluentAssertions;
using MessagePack;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.UnitTests.Queries;

public sealed class FundSelectionCatalogTests
{
    static LookupDefinitionReadModel Row(string group, string value, bool enabled = true) => new(1, group, value, value, "", 10, enabled, DateTime.UtcNow, DateTime.UtcNow);
    static IReferenceQueryApi Queries()
    {
        var queries = Substitute.For<IReferenceQueryApi>();
        queries.GetLookupDefinitionsAsync(LookupDefinitionGroups.AssetTypes, Arg.Any<CancellationToken>()).Returns(new ServiceOk<LookupDefinitionReadModel[]>([Row(LookupDefinitionGroups.AssetTypes, "Futures")]));
        queries.GetLookupDefinitionsAsync(LookupDefinitionGroups.Directions, Arg.Any<CancellationToken>()).Returns(new ServiceOk<LookupDefinitionReadModel[]>([Row(LookupDefinitionGroups.Directions, "Bullish")]));
        queries.GetLookupDefinitionsAsync(LookupDefinitionGroups.MarketConditions, Arg.Any<CancellationToken>()).Returns(new ServiceOk<LookupDefinitionReadModel[]>([Row(LookupDefinitionGroups.MarketConditions, "RangeBound")]));
        queries.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.Futures, Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([Product("ES")]));
        queries.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.FuturesOption, Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([Product("ES"), Product("NQ")]));
        return queries;
    }
    static TradeStrategySymbolReadModel Product(string symbol) => new() { Id = 1, Symbol = symbol, Exchange = "XCME", Currency = "USD", Description = symbol };

    [Fact]
    public async Task Underlyings_merge_both_catalogs_and_deduplicate_roots()
    {
        var catalog = await FundSelectionCatalog.LoadAsync(Queries());
        catalog.Underlyings.Should().Equal("ES", "NQ");
        catalog.ValidateSelections(["ES"], ["Futures"], ["Bullish"], ["RangeBound"]);
        Action invalid = () => catalog.ValidateSelections(["invented"], ["Futures"], [], []);
        invalid.Should().Throw<ArgumentException>();
        Action duplicate = () => catalog.ValidateSelections(["ES", "ES"], ["Futures"], [], []);
        duplicate.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task A_provider_failure_cannot_be_presented_as_a_complete_symbol_list()
    {
        var queries = Queries();
        queries.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.FuturesOption, Arg.Any<CancellationToken>()).Returns(new ServiceFailed<TradeStrategySymbolReadModel[]>(503, "snapshot missing"));
        await FluentActions.Invoking(() => FundSelectionCatalog.LoadAsync(queries)).Should().ThrowAsync<InvalidOperationException>().WithMessage("*snapshot missing*");
    }

    [Fact]
    public void Disabled_and_unsupported_values_cannot_authorize_a_fund()
    {
        var catalog = new FundSelectionCatalog(["ES"], [Row(LookupDefinitionGroups.AssetTypes, "Futures")],
            [Row(LookupDefinitionGroups.Directions, "Bullish", false)], [Row(LookupDefinitionGroups.MarketConditions, "FutureUnsupportedCondition")]);
        Action disabled = () => catalog.ValidateSelections(["ES"], ["Futures"], ["Bullish"], []);
        Action unknown = () => catalog.ValidateSelections(["ES"], ["Futures"], [], ["FutureUnsupportedCondition"]);
        disabled.Should().Throw<ArgumentException>(); unknown.Should().Throw<ArgumentException>();
        FundSelectionCatalog.IsSelectable(Row(LookupDefinitionGroups.Directions, "1")).Should().BeFalse();
    }

    [Fact]
    public void Lookup_query_and_rows_round_trip_through_messagepack()
    {
        var query = new GetLookupDefinitionsQuery { GroupName = LookupDefinitionGroups.AssetTypes };
        MessagePackSerializer.Deserialize<GetLookupDefinitionsQuery>(MessagePackSerializer.Serialize(query)).GroupName.Should().Be(query.GroupName);
        var row = Row(LookupDefinitionGroups.MarketConditions, "RangeBound");
        MessagePackSerializer.Deserialize<LookupDefinitionReadModel>(MessagePackSerializer.Serialize(row)).Should().Be(row);
    }
}
