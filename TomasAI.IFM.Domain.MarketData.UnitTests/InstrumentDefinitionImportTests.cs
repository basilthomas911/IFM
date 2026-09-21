using System.Globalization;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class InstrumentDefinitionImportTests
{
    static readonly DateTimeOffset At = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    static InstrumentDefinitionSelection Definition(string kind = "F") => new()
    {
        SnapshotId = Guid.NewGuid(), Dataset = "GLBX.MDP3", PublisherId = 1, InstrumentId = kind == "F" ? 99u : 42u,
        RawSymbol = kind == "F" ? "ESU6" : "ESU6 C6500.5", Root = "ES", InstrumentClass = kind,
        Currency = "USD", Exchange = "XCME", Strike = kind == "F" ? null : 6500.5m, Multiplier = 50, TickSize = .25m,
        ExpirationUtc = new(2026, 9, 18, 20, 0, 0, TimeSpan.Zero), DefinitionTimestampUtc = At,
        DefinitionDigest = new('a', 64), RawDefinitionReference = "fixture/42", UnderlyingInstrumentId = 99
    };
    [Fact]
    public void Given_provider_future_and_option_when_imported_then_exact_fraction_and_underlying_survive()
    {
        var future = InstrumentDefinitionImport.Future(Definition(), "America/Chicago", At);
        var option = InstrumentDefinitionImport.Option(Definition("C"), future, "America/Chicago", At);
        Assert.Equal("ES20260918", future.ContractId);
        Assert.Equal("ES20260918C6500.5", option.ContractId);
        Assert.Equal(6500.5m, option.StrikePriceDecimal);
        Assert.Equal(future.ContractId, option.UnderlyingContractId);
        Assert.Equal(ReferenceReviewState.Draft, option.ReviewState);
        Assert.Equal(ReferenceExerciseStyle.Unknown, option.ExerciseStyle);
        Assert.Null(option.LastTradingUtc);
        Assert.NotEmpty(FuturesReferenceQualification.Errors(option));
        Assert.Equal(option, MessagePackBinarySerializer.Shared.Deserialize<FuturesOptionContractReadModel>(
            MessagePackBinarySerializer.Shared.Serialize(option)!));
    }
    [Fact]
    public void Missing_wrong_underlying_deleted_or_expired_definition_blocks_import()
    {
        var definition = Definition("C");
        var future = InstrumentDefinitionImport.Future(Definition(), "America/Chicago", At);
        Assert.Throws<ArgumentException>(() => InstrumentDefinitionImport.Option(definition, future with { InstrumentId = 100 }, "America/Chicago", At));
        Assert.Throws<ArgumentException>(() => InstrumentDefinitionImport.Option(definition with { Deleted = true }, future, "America/Chicago", At));
        Assert.Throws<ArgumentException>(() => InstrumentDefinitionImport.Option(definition, future, "America/Chicago", At.AddYears(1)));
        Assert.Throws<ArgumentException>(() => InstrumentDefinitionImport.Option(definition with { Strike = null }, future, "America/Chicago", At));
    }
    [Fact]
    public void Query_parameter_serialization_is_bounded_and_does_not_recurse()
    {
        var request = new InstrumentDefinitionPageRequest { Root = "ES", Options = true, MinimumStrike = 6500.5m };
        Assert.StartsWith("request=", request.QueryParams);
        Assert.DoesNotContain("QueryParams", request.QueryParams);
        Assert.Throws<ArgumentException>(() => (request with { PageSize = 10000 }).Validate());
    }
}
