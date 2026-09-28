using Microsoft.Extensions.Logging;
using NSubstitute;
using StackExchange.Redis;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Caching.Redis;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.OptionPricer.IntegrationTests;

public class OptionPricerFixture : IDisposable
{
    public OptionPricerDbContext OptionPricerDb { get; private set; }
    public SequenceIdDbContext SeqIdDatabase { get; private set; }
    public ISequenceIdGenerator SequenceIdGenerator { get; private set; }
    public EventSourceActorDbContext ActorEventSourceDb { get; private set; }
    public BlackboardService BlackboardService { get; private set; } = default!;

    public OptionPricerFixture()
    {
        SetSeqIdDatabase();
        SetOptionPricerDatabase();
        SetEventSourceDatabase();
    }

    void SetOptionPricerDatabase()
    {
        var dbConn = new DbConnectionSettings()
                         .Add("OptionPricerDbConnection",
                             Environment.GetEnvironmentVariable("IFM_TEST_OPTION_PRICER_CONNECTION") ?? throw new InvalidOperationException("The assembly integration fixture did not set IFM_TEST_OPTION_PRICER_CONNECTION."),
                             "System.Data.ScyllaDb");

        var diContainer = new Dictionary<Type, OptionPricerDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var dbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<OptionPricerDbContext>), new OptionPricerDbContext(dbConn, dbFactory, SequenceIdGenerator, logger));
        OptionPricerDb = dbFactory.OptionPricerDb as OptionPricerDbContext;
    }

    void SetSeqIdDatabase()
    {
        var dbConn = new DbConnectionSettings()
             .Add("SequenceIdDbConnection",
                 Environment.GetEnvironmentVariable("IFM_TEST_POSTGRES_CONNECTION") ?? throw new InvalidOperationException("The assembly integration fixture did not set IFM_TEST_POSTGRES_CONNECTION."),
                 "System.Data.Postgres");
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
                    .Add("EventSourceActorDbConnection",
                        Environment.GetEnvironmentVariable("IFM_TEST_POSTGRES_CONNECTION") ?? throw new InvalidOperationException("The assembly integration fixture did not set IFM_TEST_POSTGRES_CONNECTION."),
                        "System.Data.Postgres");
        var diContainer = new Dictionary<Type, EventSourceActorDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var redisUri = Environment.GetEnvironmentVariable("IFM_TEST_REDIS_URL")
            ?? throw new InvalidOperationException("The assembly integration fixture did not set IFM_TEST_REDIS_URL.");
        var connMultiplexer = ConnectionMultiplexer.Connect(redisUri);
        var redisCache = new RedisCache(connMultiplexer);
        BlackboardService = new BlackboardService(redisCache, new SystemTextJsonSerializer());
        var dbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<EventSourceActorDbContext>), new EventSourceActorDbContext(dbConn, dbFactory, BlackboardService, logger));
        ActorEventSourceDb = (dbFactory.ActorEventSourceDb as EventSourceActorDbContext)!;
    }

    public void Dispose()
    {
    }
}
