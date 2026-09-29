using FluentAssertions;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Domain.MarketData.Securities.IntegrationTests;

public sealed class StartupOptionExpiryCacheIntegrationTests(SecuritiesDatabaseFixture fixture)
    : IClassFixture<SecuritiesDatabaseFixture>
{
    [Fact]
    public async Task Startup_projection_publishes_every_ES_expiry_from_value_date_through_second_future_maturity()
    {
        var valueDate = new DateOnly(2026, 9, 28);
        var firstMaturity = new DateOnly(2026, 12, 18);
        var secondMaturity = new DateOnly(2027, 3, 19);
        var snapshot = Guid.NewGuid();
        var first = Future("ES20261218", firstMaturity, true);
        var second = Future("ES20270319", secondMaturity, false);
        var expected = new[]
        {
            valueDate,
            new DateOnly(2026, 10, 2),
            new DateOnly(2026, 10, 30),
            firstMaturity,
            secondMaturity
        };
        var definitions = new[]
        {
            FutureDefinition(snapshot, 101, firstMaturity),
            FutureDefinition(snapshot, 102, secondMaturity),
            OptionDefinition(snapshot, 201, 101, "E1A", expected[0], "C"),
            OptionDefinition(snapshot, 202, 101, "EW1", expected[1], "P"),
            OptionDefinition(snapshot, 203, 101, "EW", expected[2], "C"),
            OptionDefinition(snapshot, 204, 101, "ES", expected[3], "P"),
            OptionDefinition(snapshot, 205, 102, "ES", expected[4], "C"),
            OptionDefinition(snapshot, 206, 102, "ES", secondMaturity.AddDays(1), "C")
        };
        var store = new DefinitionStore(snapshot, definitions);
        var futures = new[] { first };
        await fixture.Db.InsertFuturesContractAsync(first);
        var storedFutures = await StoredOptionDefinitionRangeLoader.LoadFuturesAsync(
            store, "ES", new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero), CancellationToken.None);
        foreach (var storedFuture in storedFutures.Where(candidate =>
                     futures.All(existing => existing.LastTradeDate != candidate.LastTradeDate)))
            await fixture.Db.InsertFuturesContractAsync(storedFuture);
        futures = futures.Concat(storedFutures.Where(candidate =>
                futures.All(existing => existing.LastTradeDate != candidate.LastTradeDate)))
            .OrderBy(contract => contract.LastTradeDate)
            .ToArray();
        futures.Should().HaveCountGreaterThanOrEqualTo(2);
        OptionExpiryCalendarPolicy.CalculateCoverageThrough(valueDate, futures).Should().Be(secondMaturity);
        (await fixture.Db.GetFuturesContractsBySymbolAsync("ES", CancellationToken.None))
            .Should().Contain(contract => contract.LastTradeDate == secondMaturity
                                          && contract.InstrumentId == 102);
        var rows = new List<CachedOptionContractDefinitionReadModel>();

        foreach (var (family, roots) in OptionExpiryCalendarPolicy.GetRoots("ES"))
        foreach (var root in roots)
        {
            var options = await StoredOptionDefinitionRangeLoader.LoadAsync(
                store, "ES", root, valueDate, secondMaturity, futures,
                new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero), CancellationToken.None);
            rows.AddRange(options.Select(option => new CachedOptionContractDefinitionReadModel
            {
                Symbol = "ES",
                UnderlyingContractId = option.UnderlyingContractId!,
                ExpiryDate = option.ContractMonth,
                ProviderRoot = root,
                OptionFamily = family,
                Definition = option,
                RefreshedAtUtc = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc)
            }));
        }

        await fixture.Db.ReplaceOptionContractDefinitionsAsync(
            "ES", valueDate, secondMaturity, rows, CancellationToken.None);
        var actual = await fixture.Db.GetOptionContractExpiriesAsync(
            "ES", valueDate, secondMaturity, CancellationToken.None);

        actual.Should().NotBeEmpty();
        actual.Should().OnlyContain(row => row.ExpiryDate >= valueDate && row.ExpiryDate <= secondMaturity);
        actual.Select(row => row.ExpiryDate).Distinct().Order().Should().Equal(expected);
        actual.Select(row => row.ProviderRoot).Should().Contain(["E1A", "EW1", "EW", "ES"]);

        var overlappingHistoricalRequest = await fixture.Db.GetOptionContractExpiriesAsync(
            "ES", valueDate.AddDays(-30), firstMaturity, CancellationToken.None);

        overlappingHistoricalRequest.Should().NotBeEmpty();
        overlappingHistoricalRequest.Should().OnlyContain(row => row.ExpiryDate >= valueDate);
        overlappingHistoricalRequest.Should().Contain(row => row.ExpiryDate == valueDate);
    }

    static FuturesContractV3ReadModel Future(string contractId, DateOnly maturity, bool onTheRun) => new(
        contractId, contractId, "ES", contractId, "FUT", "USD", "XCME", "50", maturity, onTheRun);

    static InstrumentDefinitionSelection FutureDefinition(Guid snapshot, uint instrumentId, DateOnly maturity) => new()
    {
        SnapshotId = snapshot,
        Dataset = "GLBX.MDP3",
        PublisherId = 1,
        InstrumentId = instrumentId,
        RawSymbol = $"ES-{maturity:yyyyMMdd}",
        Root = "ES",
        InstrumentClass = "F",
        Currency = "USD",
        Exchange = "XCME",
        Multiplier = 50m,
        TickSize = 0.25m,
        ExpirationUtc = new DateTimeOffset(maturity.ToDateTime(new TimeOnly(20, 0)), TimeSpan.Zero),
        DefinitionTimestampUtc = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero),
        DefinitionDigest = new string('f', 64),
        RawDefinitionReference = $"databento:GLBX.MDP3:ES-{maturity:yyyyMMdd}"
    };

    static InstrumentDefinitionSelection OptionDefinition(
        Guid snapshot, uint instrumentId, uint underlyingId, string root, DateOnly expiry, string right) => new()
    {
        SnapshotId = snapshot,
        Dataset = "GLBX.MDP3",
        PublisherId = 1,
        InstrumentId = instrumentId,
        RawSymbol = $"{root}-{expiry:yyyyMMdd}-{right}-{instrumentId}",
        Root = root,
        InstrumentClass = right,
        Currency = "USD",
        Exchange = "XCME",
        UnderlyingInstrumentId = underlyingId,
        Strike = 5000m + instrumentId,
        Multiplier = 50m,
        TickSize = 0.25m,
        ExpirationUtc = new DateTimeOffset(expiry.ToDateTime(new TimeOnly(20, 0)), TimeSpan.Zero),
        DefinitionTimestampUtc = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero),
        DefinitionDigest = new string('a', 64),
        RawDefinitionReference = $"databento:GLBX.MDP3:{root}:{instrumentId}"
    };

    sealed class DefinitionStore(Guid snapshot, InstrumentDefinitionSelection[] definitions)
        : IInstrumentDefinitionStore
    {
        public Task<InstrumentDefinitionSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken) =>
            Task.FromResult<InstrumentDefinitionSnapshot?>(new(snapshot, DateTime.UtcNow, definitions.Length, ["GLBX.MDP3"]));

        public Task<InstrumentDefinitionPage> GetSelectionPageAsync(
            InstrumentDefinitionPageRequest request, DateTimeOffset at, CancellationToken cancellationToken)
        {
            var rows = definitions.Where(definition => definition.Root == request.Root
                && (definition.InstrumentClass != "F") == request.Options).ToArray();
            return Task.FromResult(new InstrumentDefinitionPage(snapshot, DateTime.UtcNow, rows, null));
        }

        public Task IndexSelectionAsync(InstrumentDefinitionSelection definition, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task InsertAsync(Guid value, long index, ExactInstrumentDefinition definition, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task PublishAsync(InstrumentDefinitionSnapshot value, IReadOnlyCollection<TradeStrategyProduct> products, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TradeStrategySymbolReadModel[]> GetSymbolsAsync(Guid value, TradeStrategyFamilyType family, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<TradeStrategyProduct>> GetProductsAsync(Guid value, TradeStrategyFamilyType family, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
