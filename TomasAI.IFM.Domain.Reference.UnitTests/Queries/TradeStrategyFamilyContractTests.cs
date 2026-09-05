using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.UnitTests.Queries;

public sealed class TradeStrategyFamilyContractTests
{
    [Fact]
    [Trait("Gate", "PF-22")]
    public void Seed_contains_the_three_typed_ES_USD_definitions()
    {
        TradeStrategyFamilySeed.Definitions.Select(x => (x.SystemKey, x.TimeFrame)).Should().Equal(
            ("Futures-Futures", TimeFrameType.Daily),
            ("FuturesOption-VerticalSpread", TimeFrameType.Weekly),
            ("FuturesOption-IronCondor", TimeFrameType.Monthly));
        TradeStrategyFamilySeed.Definitions.Should().OnlyContain(x => x.Symbol == "ES" && x.Currency == "USD");
        TradeStrategyFamilySeed.Definitions.Select(x => x.SystemKey)
            .Should().NotContain(x => x.Contains("LONG") || x.Contains("SHORT") || x.Contains("BULL") || x.Contains("BEAR"));
    }

    [Fact]
    [Trait("Gate", "PF-22")]
    public void Exact_integer_identity_version_and_audit_round_trip()
    {
        var row = TradeStrategyFamilySeed.Definitions[0].Create(71, Now, "ReferenceBootstrap");

        var copy = MessagePackSerializer.Deserialize<TradeStrategyFamilyReadModel>(MessagePackSerializer.Serialize(row));

        copy.Should().BeEquivalentTo(row);
        copy.Validate().Should().BeEmpty();
        TradeStrategyFamilySeed.Validate(TradeStrategyFamilySeed.Definitions.Select((x, i) => x.Create(71 + i, Now, "ReferenceBootstrap")).ToArray());
    }

    static DateTime Now => new(2026, 9, 5, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MessagePack_layout_preserves_twelve_keys_and_appends_product_identity_and_exchange()
    {
        var fields = typeof(TradeStrategyFamilyReadModel).GetProperties()
            .Select(p => (p.Name, Key: p.GetCustomAttributes(typeof(KeyAttribute), false).Cast<KeyAttribute>().Single().IntKey))
            .OrderBy(p => p.Key).ToArray();
        fields.Select(x => x.Key).Should().Equal(Enumerable.Range(0, 14).Select(x => (int?)x));
        fields.Select(x => x.Name).Should().Equal("TradeStrategyFamilyId", "DefinitionVersion", "SystemKey", "Family", "Strategy", "TimeFrame", "Symbol", "Currency", "Description", "State", "CreatedOnUtc", "CreatedBy", "TradeStrategySymbolId", "Exchange");
        var bytes = MessagePackSerializer.Serialize(TradeStrategyFamilySeed.Definitions[1].Create(81, Now, "test"));
        var reader = new MessagePackReader(bytes);
        reader.ReadArrayHeader().Should().Be(14);
        reader.ReadInt32().Should().Be(81);
        reader.ReadInt64().Should().Be(1);
        reader.ReadString().Should().Be("FuturesOption-VerticalSpread");
        reader.ReadInt32().Should().Be((int)TradeStrategyFamilyType.FuturesOption);
        reader.ReadInt32().Should().Be((int)TradeStrategyType.VerticalSpread);
        reader.ReadInt32().Should().Be((int)TimeFrameType.Weekly);
    }

    [Fact]
    public void Legacy_wire_layout_is_rejected_not_misinterpreted()
    {
        var bytes = MessagePackSerializer.Serialize(new object[] { 71, 1L, "FUTURES", "Futures", 1, Now, "legacy" });
        var read = () => MessagePackSerializer.Deserialize<TradeStrategyFamilyReadModel>(bytes);
        read.Should().Throw<MessagePackSerializationException>();
    }

    [Theory]
    [InlineData("FUTURES")]
    [InlineData("FuturesOption-VerticalSpread")]
    [InlineData("futures-futures")]
    [InlineData("")]
    public void System_key_must_match_exact_family_and_strategy(string key) =>
        (TradeStrategyFamilySeed.Definitions[0].Create(71, Now, "test") with { SystemKey = key }).Validate().Should().NotBeEmpty();

    [Theory]
    [InlineData(TimeFrameType.Daily, true)]
    [InlineData(TimeFrameType.Weekly, true)]
    [InlineData(TimeFrameType.Monthly, true)]
    [InlineData(TimeFrameType.None, false)]
    [InlineData(TimeFrameType.Quarterly, false)]
    [InlineData(TimeFrameType.OneMinute, false)]
    [InlineData((TimeFrameType)999, false)]
    public void Only_three_strategy_timeframes_are_valid(TimeFrameType timeFrame, bool valid) =>
        (TradeStrategyFamilySeed.Definitions[0].Create(71, Now, "test") with { TimeFrame = timeFrame }).Validate().Any().Should().Be(!valid);

    [Theory]
    [InlineData("1")]
    [InlineData("Quarterly")]
    [InlineData("OneMinute")]
    [InlineData("daily")]
    [InlineData(null)]
    public void UI_name_mapping_rejects_non_list_names(string? name) =>
        TradeStrategyTimeFrames.TryParseName(name, out _).Should().BeFalse();

    [Fact]
    public void UI_name_mapping_is_exact_and_enum_values_are_stable()
    {
        TradeStrategyTimeFrames.Allowed.Select(x => x.ToString()).Should().Equal("Daily", "Weekly", "Monthly");
        foreach (var value in TradeStrategyTimeFrames.Allowed)
        {
            TradeStrategyTimeFrames.TryParseName(value.ToString(), out var mapped).Should().BeTrue();
            mapped.Should().Be(value);
        }
        Enum.GetValues<TradeStrategyFamilyType>().Select(x => (int)x).Should().Equal(0, 1, 2, 3, 4, 5, 6);
        Enum.GetValues<TradeStrategyType>().Select(x => (int)x).Should().Equal(0, 1, 2, 3);
    }

    [Fact]
    public void Invalid_metadata_and_duplicate_catalog_ids_fail()
    {
        var row = TradeStrategyFamilySeed.Definitions[0].Create(71, Now, "test");
        var invalid = new[] { row with { Family = (TradeStrategyFamilyType)99 }, row with { Strategy = TradeStrategyType.Unknown },
            row with { State = (TradeStrategyFamilyState)99 }, row with { Symbol = " " }, row with { Currency = "usd" },
            row with { Description = "" }, row with { CreatedOnUtc = default }, row with { CreatedBy = "" } };
        invalid.Should().OnlyContain(x => x.Validate().Count > 0);
        var validate = () => TradeStrategyFamilySeed.Validate(TradeStrategyFamilySeed.Definitions.Select(x => x.Create(71, Now, "test")).ToArray());
        validate.Should().Throw<InvalidOperationException>();
    }
}
