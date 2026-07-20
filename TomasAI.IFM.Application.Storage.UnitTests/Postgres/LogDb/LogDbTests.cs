using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using NSubstitute;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.Postgres.LogDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Application.Storage.UnitTests.Postgres.LogDb;

public class LogFixture : IDisposable
{
    public LogFixture()
    {
        SetSeqIdDatabase();
        SetDevDatabase();
    }

    void SetDevDatabase()
    {
        var dbConn = new DbConnectionSettings()
            .Add("LogDbConnection", "Host=localhost;Port=5432;Username=postgres;Password=monkey35907;Database=log-test-db", "System.Data.Postgres");
        var diContainer = new Dictionary<Type, LogDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var redisCache = Substitute.For<IRedisCache>();
        var redisCacheMap = new Dictionary<string, string>();
        redisCache.Get(Arg.Any<string>()).Returns(callInfo => redisCacheMap[callInfo.Arg<string>()]);
        redisCache.TryGet(Arg.Any<string>(), out Arg.Any<string>()).Returns(_ => {
            if (redisCacheMap.ContainsKey(_.ArgAt<string>(0)))
                _[1] = redisCacheMap[_.ArgAt<string>(0)];
            return redisCacheMap.ContainsKey(_.ArgAt<string>(0));
        });
        redisCache.When(_ => _.Set(Arg.Any<string>(), Arg.Any<string>())).Do(_ => { redisCacheMap.Add(_.ArgAt<string>(0), _.ArgAt<string>(1)); });
        var blackboardServce = new BlackboardService(redisCache, new SystemTextJsonSerializer());
        DbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<LogDbContext>), new LogDbContext(dbConn, DbFactory, SequenceIdGenerator, logger));
        Db = DbFactory.LogDb as LogDbContext;
    }

    void SetSeqIdDatabase()
    {
        var dbConn = new DbConnectionSettings()
             .Add("SequenceIdDbConnection", "Host=localhost;Port=5432;Username=postgres;Password=monkey35907;Database=sequence-id-test-db", "System.Data.Postgres");
        var diContainer = new Dictionary<Type, SequenceIdDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var dbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<SequenceIdDbContext>), new SequenceIdDbContext(dbConn, dbFactory, logger));
        SeqIdDatabase = dbFactory.SequenceIdDb as SequenceIdDbContext;
        SequenceIdGenerator = new PostgresSequenceIdGenerator(dbFactory.SequenceIdDb as SequenceIdDbContext);
    }

    public LogDbContext Db { get;  private set; }

    public IDbContextFactory DbFactory { get; private set; }

    public ISequenceIdGenerator SequenceIdGenerator { get; private set; }
    public SequenceIdDbContext SeqIdDatabase { get; private set; }

    public void Dispose()
    {
    }
}

public class LogDbTests : IClassFixture<LogFixture>
{
    private readonly LogFixture _testFixture;

    public LogDbTests(LogFixture testFixture)
    {
        _testFixture = testFixture;
    }

    [Fact]
    public async Task GetTelemetryLogsByDateRangeAsync()
    {
        var startDate = SampleData.LogEvent.Timestamp.AddDays(-1);
        var endDate = SampleData.LogEvent.Timestamp.AddDays(1);
        var db = _testFixture.DbFactory.LogDb as LogDbContext;
        await db.Use("delete from telemetry_log").ExecuteCommandAsync();
        await db.InsertTelemetryLogsAsync( [SampleData.LogEvent]);
        var result = await db.GetTelemetryLogsByDateRangeAsync(startDate, endDate);
        result.Should().NotBeNull();
        result.Count.Should().Be(1);
        result.First().LogLevel.Should().Be(SampleData.LogEvent.LogLevel);
        result.First().Message.Should().Be(SampleData.LogEvent.Message);
        result.First().ServiceId.Should().Be(SampleData.LogEvent.ServiceId);
    }
}
