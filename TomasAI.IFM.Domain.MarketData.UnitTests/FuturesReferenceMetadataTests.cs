using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class FuturesReferenceMetadataTests
{
    [Theory]
    [InlineData("6500.5", true)]
    [InlineData("6500.500", true)]
    [InlineData("0", false)]
    [InlineData("6500,5", false)]
    [InlineData("6500.12345678901234567890123456789", false)]
    public void Editor_strike_parser_requires_exact_positive_invariant_decimal(string text, bool valid)
        => Assert.Equal(valid, Shared.FuturesOptionContractId.TryParseStrike(text, out _));

    [Fact]
    public void Local_symbol_retains_fractional_strike()
    {
        Assert.Equal("ESU6 C6500.5", FuturesOptionContractReadModel.GetExactContractLocalSymbol("ESU6", "Call", 6500.5m));
        Assert.Equal("ESU6 C6500.5", FuturesOptionContractReadModel.GetContractLocalSymbol("ESU6", "Call", 6500.5));
    }

    static FuturesOptionContractReadModel Option() => new("ES20260918C6500.5", "fixture", "ES", "E3BU6 C6500.5",
        "FOP", "USD", "CME", "50", new(2026, 9, 18), 6500.5, "Call")
    {
        StrikePriceDecimal = 6500.5m, SchemaVersion = 1, ReviewState = ReferenceReviewState.Reviewed,
        Dataset = "GLBX.MDP3", PublisherId = 1, InstrumentId = 42, RawSymbol = "E3BU6 C6500.5",
        DefinitionTimestampUtc = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
        DefinitionDigest = new('a', 64), RawDefinitionReference = "fixture/definition/42",
        ExpirationUtc = new(2026, 9, 18, 20, 0, 0, TimeSpan.Zero),
        LastTradingUtc = new(2026, 9, 18, 20, 0, 0, TimeSpan.Zero), ExchangeTimeZoneId = "America/New_York",
        SettlementStyle = ReferenceSettlementStyle.DeliveryOfFuture, MultiplierValue = 50, PriceScale = 1,
        TickSize = .25m, CalendarVersion = "fixture/calendar", MappingVersion = "fixture/v1", EvidenceId = "fixture/review",
        EffectiveFromUtc = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
        EffectiveUntilUtc = new(2026, 9, 18, 20, 0, 0, TimeSpan.Zero), UnderlyingContractId = "ES20260918",
        UnderlyingAssetType = ReferenceAssetType.Futures, OptionRight = ReferenceOptionRight.Call,
        UnderlyingInstrumentId = 99, UnderlyingPublisherId = 1,
        ExerciseStyle = ReferenceExerciseStyle.American, PremiumStyle = ReferencePremiumStyle.PremiumPaid,
        ExerciseCutoffUtc = new(2026, 9, 18, 20, 0, 0, TimeSpan.Zero), ExerciseResultContractId = "ES20260918",
        PremiumTickRule = ReferencePremiumTickRule.Fixed, TickRuleVersion = "fixture/ticks", DayCount = ReferenceDayCount.Actual365Fixed
    };

    [Fact]
    public void Appended_option_metadata_round_trips_and_legacy_reader_keeps_original_keys()
    {
        var value = Option();
        var bytes = MessagePackBinarySerializer.Shared.Serialize(value)!;
        Assert.Equal(value, MessagePackBinarySerializer.Shared.Deserialize<FuturesOptionContractReadModel>(bytes));
        var legacy = MessagePackSerializer.Deserialize<LegacyOptionReference>(bytes, MessagePackBinarySerializer.Options);
        Assert.Equal(value.ContractId, legacy.ContractId);
        Assert.Equal(6500.5, legacy.StrikePrice);
        var oldBytes = MessagePackSerializer.Serialize(legacy, MessagePackBinarySerializer.Options);
        var translated = MessagePackBinarySerializer.Shared.Deserialize<FuturesOptionContractReadModel>(oldBytes)!;
        Assert.Null(translated.StrikePriceDecimal);
        Assert.Equal(6500.5m, translated.GetExactStrikePrice());
        Assert.Equal(ReferenceExerciseStyle.Unknown, translated.ExerciseStyle);
        Assert.Equal(ReferenceReviewState.Unknown, translated.ReviewState);
        Assert.NotEmpty(FuturesReferenceQualification.Errors(translated));
        Assert.Equal(value.ContractId, translated.Id.ContractId);
    }

    [Fact]
    public void Future_metadata_survives_existing_serialization_constructor()
    {
        var value = new FuturesContractV3ReadModel("ES20260918", "fixture", "ES", "ESU6", "FUT", "USD", "CME", "50",
            new(2026, 9, 18), false)
        {
            SchemaVersion = 1, ReviewState = ReferenceReviewState.Draft, Dataset = "GLBX.MDP3", PublisherId = 1,
            InstrumentId = 99, RawSymbol = "ESU6", DefinitionTimestampUtc = DateTimeOffset.UnixEpoch,
            DefinitionDigest = new('b', 64), RawDefinitionReference = "fixture/99", MultiplierValue = 50,
            PriceScale = 1, TickSize = .25m, MappingVersion = "v1"
        };
        Assert.Equal(value, MessagePackBinarySerializer.Shared.Deserialize<FuturesContractV3ReadModel>(
            MessagePackBinarySerializer.Shared.Serialize(value)!));
        Assert.Equal(value, ReferencePayloadCodec.ReadFuture(ReferencePayloadCodec.Write(value), value));
    }

    [Fact]
    public void Reviewed_unknown_or_conflicting_metadata_cannot_qualify()
    {
        var value = Option();
        Assert.Empty(FuturesReferenceQualification.Errors(value));
        Assert.NotEmpty(FuturesReferenceQualification.Errors(value with { ExerciseStyle = ReferenceExerciseStyle.Unknown }));
        Assert.NotEmpty(FuturesReferenceQualification.Errors(value with { UnderlyingAssetType = ReferenceAssetType.Equity }));
        Assert.NotEmpty(FuturesReferenceQualification.Errors(value with { ReviewState = ReferenceReviewState.Draft }));
        Assert.NotEmpty(FuturesReferenceQualification.Errors(value with { StrikePriceDecimal = 6500.6m }));
        Assert.NotEmpty(FuturesReferenceQualification.Errors(value with { MultiplierValue = 100 }));
        Assert.Throws<InvalidDataException>(() => (value with { StrikePriceDecimal = 6500.6m }).GetExactStrikePrice());
    }

    [Fact]
    public void Storage_rejects_truncated_nil_or_mixed_version_payload_instead_of_returning_qualified_data()
    {
        var value = Option();
        var bytes = ReferencePayloadCodec.Write(value);
        Assert.Equal(value, ReferencePayloadCodec.ReadOption(bytes, value));
        Assert.Throws<InvalidDataException>(() => ReferencePayloadCodec.ReadOption(bytes, value with { StrikePrice = 6600 }));
        Assert.Throws<InvalidDataException>(() => ReferencePayloadCodec.ReadOption(bytes[..^1], value));
        Assert.Throws<InvalidDataException>(() => ReferencePayloadCodec.ReadOption([0xc0], value));
        Assert.Same(value, ReferencePayloadCodec.ReadOption(null, value));
    }

    [Fact]
    public void Canonical_decimal_retains_precision_not_representable_in_legacy_double()
    {
        const decimal exact = 6500.1234567890123456789012345m;
        var value = Option() with { StrikePrice = (double)exact, StrikePriceDecimal = exact };
        var copy = ReferencePayloadCodec.ReadOption(ReferencePayloadCodec.Write(value), value);
        Assert.Equal(exact, copy.GetExactStrikePrice());
    }
}

[MessagePackObject]
public sealed record LegacyOptionReference(
    [property: Key(0)] string ContractId, [property: Key(1)] string Description,
    [property: Key(2)] string Symbol, [property: Key(3)] string LocalSymbol,
    [property: Key(4)] string SecurityType, [property: Key(5)] string Currency,
    [property: Key(6)] string Exchange, [property: Key(7)] string Multiplier,
    [property: Key(8)] DateOnly ContractMonth, [property: Key(9)] double StrikePrice,
    [property: Key(10)] string OptionType);
