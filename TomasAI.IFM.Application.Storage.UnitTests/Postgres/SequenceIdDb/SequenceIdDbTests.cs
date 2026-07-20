using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using NSubstitute;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.UnitTests.Postgres.SequenceIdDb;

public class SequenceIdFixture : IDisposable
{

    public SequenceIdFixture()
    {
        var dbConn = new DbConnectionSettings()
             .Add("SequenceIdDbConnection", "Host=localhost;Port=5432;Username=postgres;Password=monkey35907;Database=sequence-id-test-db", "System.Data.Postgres");
        var diContainer = new Dictionary<Type, SequenceIdDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        DbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<SequenceIdDbContext>), new SequenceIdDbContext(dbConn, DbFactory,  logger));
        Db = DbFactory.SequenceIdDb as SequenceIdDbContext;
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
}
