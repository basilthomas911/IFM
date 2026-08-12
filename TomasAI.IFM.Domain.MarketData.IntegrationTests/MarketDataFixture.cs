using Microsoft.Extensions.Logging;
using NSubstitute;
using System;
using System.Collections.Generic;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using StackExchange.Redis;
using TomasAI.IFM.Framework.Caching.Redis;

namespace TomasAI.IFM.Domain.MarketData.IntegrationTests;

public class MarketDataFixture : IDisposable
{
    public DbContextFactory DbFactory { get; private set; }
    public IMarketDataDbContext MarketDataDb { get; private set; }
    public SequenceIdDbContext SeqIdDatabase { get; private set; }
    public ISequenceIdGenerator SequenceIdGenerator { get; private set; }
    public EventSourceActorDbContext ActorEventSourceDb { get; private set; } = default!;
    public BlackboardService BlackboardService { get; private set; } = default!;

    public MarketDataFixture()
    {
        SetSeqIdDatabase();
        SetDbFactory();
        SetEventSourceDatabase();
    }

    void SetEventSourceDatabase()
    {
        var settings = new DbConnectionSettings()
            .Add("EventSourceActorDbConnection", "Host=localhost;Port=5432;Database=event-source-test-db", "System.Data.Postgres");
        var repositories = new Dictionary<Type, EventSourceActorDbContext>();
        var factory = new DbContextFactory(new DbContextResolver(type => repositories[type]));
        var redisCache = new RedisCache(ConnectionMultiplexer.Connect("localhost:6379"));
        BlackboardService = new BlackboardService(redisCache, new SystemTextJsonSerializer());
        var logger = Substitute.For<ILogger<DbProvider>>();
        repositories.Add(typeof(IObjectRepository<EventSourceActorDbContext>),
            new EventSourceActorDbContext(settings, factory, BlackboardService, logger));
        ActorEventSourceDb = (EventSourceActorDbContext)factory.ActorEventSourceDb;
    }

    void SetDbFactory()
    {
        var dbConn = new DbConnectionSettings()
             .Add("MarketDataDbConnection", "Contact Points=localhost;Port=9042;Default Keyspace=market_data_test_db", "System.Data.ScyllaDb");
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
        new MarketDataSchemaDb(dbConn, logger)
            .CreateAsync(["economic_calendar", "economic_calendar_by_country_month_v2"])
            .GetAwaiter().GetResult();
        diContainer.Add(typeof(IObjectRepository<MarketDataDbContext>), new MarketDataDbContext(dbConn, dbFactory, blackboardService, SequenceIdGenerator, logger));
        diContainer.Add(typeof(IObjectRepository<SecuritiesDbContext>), SeqIdDatabase);
        DbFactory = dbFactory;
        MarketDataDb = dbFactory.MarketDataDb as IMarketDataDbContext;
    }

    void SetSeqIdDatabase()
    {
        var dbConn = new DbConnectionSettings()
             .Add("SequenceIdDbConnection", "Host=localhost;Port=5432;Database=sequence-id-test-db", "System.Data.Postgres");
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

