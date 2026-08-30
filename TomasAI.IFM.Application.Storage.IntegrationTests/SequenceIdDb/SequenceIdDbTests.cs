using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using NSubstitute;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Portfolio.Identity;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.SequenceIdDb;

public class SequenceIdFixture : IDisposable
{

    public SequenceIdFixture()
    {
        var dbConn = new DbConnectionSettings()
             .Add("SequenceIdDbConnection", "Host=localhost;Port=5432;Database=sequence-id-test-db", "System.Data.Postgres");
        var diContainer = new Dictionary<Type, SequenceIdDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        DbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<SequenceIdDbContext>), new SequenceIdDbContext(dbConn, DbFactory,  logger));
        Db = DbFactory.SequenceIdDb as SequenceIdDbContext;
        SequenceIdDatabaseInitializer.EnsureInitialized(new TomasAI.IFM.Application.Storage.SequenceIdDb.Schema.SequenceIdSchemaDb(dbConn, logger));
        SequenceIdGenerator = new PostgresSequenceIdGenerator(Db);
    }
    public SequenceIdDbContext Db { get; }

    public IDbContextFactory DbFactory { get; }
    public ISequenceIdGenerator SequenceIdGenerator { get; }

    public void Dispose()
    {
    }
}

public class SequenceIdDbTests : IClassFixture<SequenceIdFixture>
{
    readonly SequenceIdFixture _testFixture;

    public SequenceIdDbTests(SequenceIdFixture testFixture)
    {
        _testFixture = testFixture;
    }

    [Fact]
    public async Task GetNextSequenceId_Ok()
    {
        var db = _testFixture.DbFactory.SequenceIdDb as ISequenceIdDbContext;
        var sequenceId = await db.GetNextSequenceIdAsync(SequenceName.FuturesTickData_TickId);
        var nextSequenceId = await db.GetNextSequenceIdAsync(SequenceName.FuturesTickData_TickId);
        nextSequenceId.Should().Be(sequenceId+100);
    }

    [Fact]
    public async Task GetCurrentSequenceId_Ok()
    {
         var db = _testFixture.DbFactory.SequenceIdDb as ISequenceIdDbContext;
        var curSequenceId = await _testFixture.SequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesTickData_TickId);
        var nextSequenceId = await _testFixture.SequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesTickData_TickId);
        nextSequenceId.Should().Be(curSequenceId + 1);
    }

    [Fact]
    public async Task MultipleGeneratorInstancesReserveDisjointPostgresRanges()
    {
        var firstGenerator = new PostgresSequenceIdGenerator(_testFixture.Db);
        var secondGenerator = new PostgresSequenceIdGenerator(_testFixture.Db);
        var sequenceName = SequenceName.FuturesItiTrendDeltaData_SequenceId;

        var first = Enumerable.Range(0, 250)
            .Select(async _ => await firstGenerator.GetSequenceIdAsync(sequenceName));
        var second = Enumerable.Range(0, 250)
            .Select(async _ => await secondGenerator.GetSequenceIdAsync(sequenceName));
        var sequenceIds = await Task.WhenAll(first.Concat(second));

        sequenceIds.Should().OnlyHaveUniqueItems();
        (await firstGenerator.GetHighWatermarkAsync(sequenceName))
            .Should().BeGreaterThanOrEqualTo(sequenceIds.Max());
    }

    [Fact]
    [Trait("Gate", "PF-02")]
    [Trait("Category", "Portfolio")]
    public async Task PortfolioBusinessIdsUseTheirAuthoritativePostgresSequences()
    {
        var allocator = new PortfolioBusinessIdAllocator(_testFixture.SequenceIdGenerator);

        var portfolioId = await allocator.AllocatePortfolioIdAsync();
        var fundId = await allocator.AllocateFundIdAsync();
        var orderId = await allocator.AllocateOrderIdAsync();
        var tradeId = await allocator.AllocateTradeIdAsync();

        portfolioId.Id.Should().BePositive();
        fundId.Should().BePositive();
        orderId.Should().BePositive();
        tradeId.Should().BePositive();
    }
}
