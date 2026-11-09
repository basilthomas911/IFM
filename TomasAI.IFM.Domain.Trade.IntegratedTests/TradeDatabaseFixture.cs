using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests;

public class TradeDatabaseFixture : IDisposable
{
    public TradeDbContext TradeDb { get; private set; }
    public SequenceIdDbContext SeqIdDatabase { get; private set; }
    public ISequenceIdGenerator SequenceIdGenerator { get; private set; }
    public EventSourceActorDbContext ActorEventSourceDb { get; private set; }


    public IDbContextFactory DbFactory { get; private set; }

    public TradeDatabaseFixture()
    {
        EventLogEngineQualification.Validate();
        SetSeqIdDatabase();
        SetTradeDatabase();
        SetEventSourceDatabase();
    }

    void SetTradeDatabase()
    {
        var dbConn = new DbConnectionSettings()
                         .Add("TradeDbConnection", Environment.GetEnvironmentVariable("IFM_TEST_TRADE_CONNECTION") ?? "Host=localhost;Port=5432;Database=trade-test-db", "System.Data.Postgres");

        var diContainer = new Dictionary<Type, TradeDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });


        DbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<TradeDbContext>), new TradeDbContext(dbConn, DbFactory, SequenceIdGenerator, logger));
        TradeDb = DbFactory.TradeDb as TradeDbContext;
    }

    void SetSeqIdDatabase()
    {
        var dbConn = new DbConnectionSettings()
             .Add("SequenceIdDbConnection", Environment.GetEnvironmentVariable("IFM_TEST_POSTGRES_CONNECTION") ?? "Host=localhost;Port=5432;Database=sequence-id-test-db", "System.Data.Postgres");
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
                    .Add("EventSourceActorDbConnection", Environment.GetEnvironmentVariable("IFM_TEST_POSTGRES_CONNECTION") ?? "Host=localhost;Port=5432;Database=event-source-test-db", "System.Data.Postgres");
        var diContainer = new Dictionary<Type, EventSourceActorDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });


        var dbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<EventSourceActorDbContext>), new EventSourceActorDbContext(dbConn, dbFactory, logger));
        ActorEventSourceDb = (dbFactory.ActorEventSourceDb as EventSourceActorDbContext)!;
    }

    public void Dispose()
    {
    }
}
