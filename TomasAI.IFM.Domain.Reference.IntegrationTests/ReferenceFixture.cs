using Microsoft.Extensions.Logging;
using NSubstitute;
using StackExchange.Redis;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.ReferenceDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Caching.Redis;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Fund.Shared;

namespace TomasAI.IFM.Domain.Reference.IntegrationTests;

public class ReferenceFixture : IDisposable
{
    public DbContextFactory DbFactory { get; private set; } = default!;
    public IReferenceDbContext ReferenceDb { get; private set; } = default!;
    public SequenceIdDbContext SeqIdDatabase { get; private set; }
    public ISequenceIdGenerator SequenceIdGenerator { get; private set; }
    public EventSourceActorDbContext ActorEventSourceDb { get; private set; }

    public ReferenceFixture()
    {
        SetDbFactory();
        SetSeqIdDatabase();
        SetEventSourceDatabase();
    }

    void SetDbFactory()
    {
        var dbConn = new DbConnectionSettings()
            .Add("ReferenceDbConnection", "Contact Points=localhost;Port=9042;Username=ifmapp;Password=monkey35907;Default Keyspace=reference_test_db", "System.Data.ScyllaDb");

         var diContainer = new Dictionary<Type, ReferenceDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var redisUri = "localhost:6379";
        var connMultiplexer = ConnectionMultiplexer.Connect(redisUri);
        var redisCache = new RedisCache(connMultiplexer);
        var blackboardServce = new BlackboardService(redisCache, new SystemTextJsonSerializer());
        DbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<ReferenceDbContext>), new ReferenceDbContext(dbConn, DbFactory, logger));
        ReferenceDb = (DbFactory.ReferenceDb as ReferenceDbContext)!;

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

    void SetEventSourceDatabase()
    {
        var dbConn = new DbConnectionSettings()
                    .Add("EventSourceActorDbConnection", "Host=localhost;Port=5432;Username=postgres;Password=monkey35907;Database=event-source-test-db", "System.Data.Postgres");
        var diContainer = new Dictionary<Type, EventSourceActorDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var redisUri = "localhost:6379";
        var connMultiplexer = ConnectionMultiplexer.Connect(redisUri);
        var redisCache = new RedisCache(connMultiplexer);
        var blackboardService = new BlackboardService(redisCache, new SystemTextJsonSerializer());
        var dbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<EventSourceActorDbContext>), new EventSourceActorDbContext(dbConn, dbFactory, blackboardService, logger));
        ActorEventSourceDb = (dbFactory.ActorEventSourceDb as EventSourceActorDbContext)!;
    }


    public void Dispose()
    {
        // Cleanup if needed
    }
}
