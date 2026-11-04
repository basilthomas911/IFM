using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.StrategyCatalog;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using Xunit;

namespace TomasAI.IFM.Domain.Reference.UnitTests.Queries;

public sealed class StrategyCatalogProductAuthorityTests
{
    [Fact]
    public async Task Product_validation_reads_one_published_snapshot_without_a_live_market_data_host()
    {
        var database = Substitute.For<IReferenceDbContext>();
        var factory = Substitute.For<IDbContextFactory>(); factory.ReferenceDb.Returns(database);
        var snapshot = new InstrumentDefinitionSnapshot(Guid.NewGuid(), DateTime.UtcNow, 1, ["GLBX.MDP3"]);
        database.GetInstrumentDefinitionSnapshotAsync(CancellationToken.None).Returns(snapshot);
        database.GetInstrumentDefinitionSymbolsAsync(snapshot.Id, TradeStrategyFamilyType.Futures, CancellationToken.None)
            .Returns([new TradeStrategySymbolReadModel { Id = 101, Symbol = "ES", Exchange = "XCME", Currency = "USD" }]);
        database.GetInstrumentDefinitionSymbolsAsync(snapshot.Id, TradeStrategyFamilyType.FuturesOption, CancellationToken.None).Returns([]);
        await new StrategyCatalogReferenceAdapter(factory).ValidateProductAsync(new(101, "ES", "XCME", "USD"), CancellationToken.None);
        await database.Received(1).GetInstrumentDefinitionSnapshotAsync(CancellationToken.None);
        await Assert.ThrowsAsync<ArgumentException>(() => new StrategyCatalogReferenceAdapter(factory)
            .ValidateProductAsync(new(101, "ES", "XCME", "CAD"), CancellationToken.None));
    }

    [Fact]
    public async Task Missing_published_reference_snapshot_fails_before_accepting_a_product()
    {
        var factory = Substitute.For<IDbContextFactory>(); factory.ReferenceDb.Returns(Substitute.For<IReferenceDbContext>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => new StrategyCatalogReferenceAdapter(factory)
            .ValidateProductAsync(new(101, "ES", "XCME", "USD"), CancellationToken.None));
    }
}
