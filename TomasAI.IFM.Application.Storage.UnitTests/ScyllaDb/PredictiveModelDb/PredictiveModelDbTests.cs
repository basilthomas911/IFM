using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.ScyllaDb.PredictiveModelDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;

namespace TomasAI.IFM.Application.Storage.UnitTests.ScyllaDb.PredictiveModelDb;

public class PredictiveModelFixture : IDisposable
{
    public PredictiveModelFixture()
    {
        SetDevDatabase();
    }

    public void Dispose()
    {
        // Do "global" teardown here; Only called once.
    }

    public PredictiveModelDbContext DevDatabase { get; private set; }

     void SetDevDatabase()
    {
        var dbConn = new DbConnectionSettings()
            .Add("PredictiveModelDbConnection", "Contact Points=localhost;Port=9042;Username=ifmapp;Password=monkey35907;Default Keyspace=predictive_model_test_db", "System.Data.ScyllaDb");
        var diContainer = new Dictionary<Type, PredictiveModelDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var redisCacheMap = new Dictionary<string, string>();
        var redisCache = Substitute.For<IRedisCache>();
        redisCache.Get(Arg.Any<string>()).Returns(callInfo => redisCacheMap[callInfo.Arg<string>()]);
        redisCache.When(_ => _.Set(Arg.Any<string>(), Arg.Any<string>())).Do(_ => { redisCacheMap.Add(_.ArgAt<string>(0), _.ArgAt<string>(1)); });
        var blackboardServce = new BlackboardService(redisCache, new SystemTextJsonSerializer());
        var dbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        diContainer.Add(typeof(IObjectRepository<PredictiveModelDbContext>), new PredictiveModelDbContext(dbConn, dbFactory, logger));
        DevDatabase = dbFactory.PredictiveModelDb as PredictiveModelDbContext;
    }

}

public class PredictiveModelDbTests(PredictiveModelFixture testFixture) : IClassFixture<PredictiveModelFixture>
{
    PredictiveModelFixture TestFixture { get; } = testFixture;

    [Fact]
    public async Task CreatePredictiveModelTablesAsync()
    {
        var db = TestFixture.DevDatabase;
        await db.CreatePredictiveModelDbTablesAsync();
    }
}
