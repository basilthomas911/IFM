using Microsoft.Extensions.Logging;
using NSubstitute;
using System;
using System.Collections.Generic;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.MarketData.IntegrationTests;

public class MarketDataFixture : IDisposable
{
    public DbContextFactory DbFactory { get; private set; }
    public IMarketDataDbContext MarketDataDb { get; private set; }
    public SequenceIdDbContext SeqIdDatabase { get; private set; }
    public ISequenceIdGenerator SequenceIdGenerator { get; private set; }

    public MarketDataFixture()
    {
        SetSeqIdDatabase();
        SetDbFactory();
    }

    void SetDbFactory()
    {
        var dbConn = new DbConnectionSettings()
             .Add("MarketDataDbConnection", "Contact Points=localhost;Port=9042;Username=ifmapp;Password=monkey35907;Default Keyspace=market_data_test_db", "System.Data.ScyllaDb");
        var diContainer = new Dictionary<Type, IObjectRepository>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var dbFactory = new DbContextFactory(dbResolver);
        var marketDataDbLogger = Substitute.For<ILogger<DbProvider>>();
        marketDataDbLogger.When(_ => { }).Do(_ => { });
        var redisCahe = Substitute.For<IRedisCache>();
        redisCahe.When(_ => { }).Do(_ => { });
        var blackboardService = new BlackboardService(redisCahe, new SystemTextJsonSerializer());
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        diContainer.Add(typeof(IObjectRepository<MarketDataDbContext>), new MarketDataDbContext(dbConn, dbFactory, blackboardService, SequenceIdGenerator, logger));
        diContainer.Add(typeof(IObjectRepository<SecuritiesDbContext>), SeqIdDatabase);
        DbFactory = dbFactory;
        MarketDataDb = dbFactory.MarketDataDb as IMarketDataDbContext;
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
    public void Dispose()
    {
    }
}

