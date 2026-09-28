using FluentAssertions;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.Pricing;
using Xunit;

namespace TomasAI.IFM.Domain.Application.UnitTests;

public sealed class OptionRefreshAlgorithmsTests
{
    static readonly FuturesContractV3ReadModel[] Futures =
    [
        Contract("ESH7", new DateOnly(2027, 3, 19)),
        Contract("ESM7", new DateOnly(2027, 6, 18)),
        Contract("ESU7", new DateOnly(2027, 9, 17))
    ];

    [Theory]
    [InlineData(2027, 1, 1, "ESH7")]
    [InlineData(2027, 3, 19, "ESH7")]
    [InlineData(2027, 3, 20, "ESM7")]
    [InlineData(2027, 12, 31, "ESU7")]
    public void Underlying_lookup_returns_first_contract_not_before_expiry(
        int year,
        int month,
        int day,
        string expected)
    {
        OptionRefreshAlgorithms.FindUnderlyingContractId(
                Futures,
                new DateOnly(year, month, day))
            .Should().Be(expected);
    }

    [Fact]
    public void Underlying_lookup_rejects_an_empty_curve()
    {
        var action = () => OptionRefreshAlgorithms.FindUnderlyingContractId(
            Array.Empty<FuturesContractV3ReadModel>(),
            new DateOnly(2027, 1, 1));

        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(17, 4)]
    [InlineData(4, 0)]
    [InlineData(4, 17)]
    public void Refresh_options_reject_invalid_concurrency(
        int providerConcurrency,
        int verificationConcurrency)
    {
        var options = new OptionContractExpiryCalendarOptions
        {
            MaximumProviderConcurrency = providerConcurrency,
            MaximumVerificationConcurrency = verificationConcurrency
        };

        var action = options.Validate;

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Stored_definition_range_loads_every_expiry_inside_the_inclusive_window()
    {
        var snapshot = Guid.NewGuid();
        var store = new DefinitionStore(snapshot,
        [
            Definition(snapshot, 10, "ESU6 C5000", "C", new DateTimeOffset(2026, 9, 27, 20, 0, 0, TimeSpan.Zero)),
            Definition(snapshot, 11, "ESU6 C5050", "C", new DateTimeOffset(2026, 9, 28, 20, 0, 0, TimeSpan.Zero)),
            Definition(snapshot, 12, "ESU6 P4950", "P", new DateTimeOffset(2026, 10, 16, 20, 0, 0, TimeSpan.Zero)),
            Definition(snapshot, 13, "ESU6 C5100", "C", new DateTimeOffset(2026, 10, 17, 20, 0, 0, TimeSpan.Zero))
        ]);
        var future = Contract("ES20261218", new DateOnly(2026, 12, 18)) with
        {
            Dataset = "GLBX.MDP3",
            PublisherId = 1,
            InstrumentId = 100,
            MultiplierValue = 1m
        };

        var result = await StoredOptionDefinitionRangeLoader.LoadAsync(
            store,
            "ES",
            "EW1",
            new DateOnly(2026, 9, 28),
            new DateOnly(2026, 10, 16),
            [future],
            new DateTimeOffset(2026, 9, 28, 0, 0, 0, TimeSpan.Zero),
            CancellationToken.None);

        result.Select(option => option.ContractMonth).Should().Equal(
            new DateOnly(2026, 9, 28),
            new DateOnly(2026, 10, 16));
        result.Select(option => option.UnderlyingContractId).Should().OnlyContain(id => id == future.ContractId);
        result.Should().OnlyContain(option => option.ReviewState == ReferenceReviewState.Reviewed);
        result.Should().OnlyContain(option =>
            option.MappingVersion == $"StoredInstrumentDefinition/v2/{new string('a', 16)}");
        result.Should().OnlyContain(option => FuturesReferenceQualification.Errors(option).Count == 0);
        result.Select(ReviewedOptionConvention.From).Should().HaveCount(2);
    }

    static InstrumentDefinitionSelection Definition(
        Guid snapshot,
        uint instrumentId,
        string rawSymbol,
        string instrumentClass,
        DateTimeOffset expiration) => new()
    {
        SnapshotId = snapshot,
        Dataset = "GLBX.MDP3",
        PublisherId = 1,
        InstrumentId = instrumentId,
        RawSymbol = rawSymbol,
        Root = "EW1",
        InstrumentClass = instrumentClass,
        Currency = "USD",
        Exchange = "XCME",
        UnderlyingInstrumentId = 100,
        Strike = instrumentClass == "C" ? 5000m + instrumentId : 4900m + instrumentId,
        Multiplier = 1m,
        TickSize = null,
        ExpirationUtc = expiration,
        DefinitionTimestampUtc = expiration.AddDays(-30),
        DefinitionDigest = new string('a', 64),
        RawDefinitionReference = $"databento:GLBX.MDP3:{rawSymbol}"
    };

    sealed class DefinitionStore(Guid snapshot, InstrumentDefinitionSelection[] definitions)
        : IInstrumentDefinitionStore
    {
        public Task<InstrumentDefinitionSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken) =>
            Task.FromResult<InstrumentDefinitionSnapshot?>(new(snapshot, DateTime.UtcNow, definitions.Length, ["GLBX.MDP3"]));

        public Task<InstrumentDefinitionPage> GetSelectionPageAsync(
            InstrumentDefinitionPageRequest request,
            DateTimeOffset at,
            CancellationToken cancellationToken) =>
            Task.FromResult(new InstrumentDefinitionPage(snapshot, DateTime.UtcNow, definitions, null));

        public Task IndexSelectionAsync(InstrumentDefinitionSelection definition, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task InsertAsync(Guid value, long index, ExactInstrumentDefinition definition, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task PublishAsync(InstrumentDefinitionSnapshot value, IReadOnlyCollection<TradeStrategyProduct> products, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<TradeStrategySymbolReadModel[]> GetSymbolsAsync(Guid value, TradeStrategyFamilyType family, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<TradeStrategyProduct>> GetProductsAsync(Guid value, TradeStrategyFamilyType family, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    static FuturesContractV3ReadModel Contract(string id, DateOnly lastTradeDate) =>
        new(id, id, "ES", id, "FUT", "USD", "CME", "50", lastTradeDate, false);
}
