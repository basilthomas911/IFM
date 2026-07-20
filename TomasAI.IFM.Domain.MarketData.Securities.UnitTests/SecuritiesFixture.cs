using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Messaging.Nats.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

using static TomasAI.IFM.Domain.MarketData.Securities.UnitTests.FuturesContract.FuturesContractCommandActorTests;
using static TomasAI.IFM.Domain.Securities.UnitTests.FuturesContract.FuturesContractQueryActorTests;
using static TomasAI.IFM.Domain.Securities.UnitTests.FuturesOptionContract.FuturesOptionContractCommandActorTests;
using static TomasAI.IFM.Domain.Securities.UnitTests.FuturesOptionContract.FuturesOptionContractQueryActorTests;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query;

namespace TomasAI.IFM.Domain.Securities.UnitTests;

public class SecuritiesFixture : IDisposable
{
    public DbContextFactory DbFactory { get; private set; } = default!;
    public ISecuritiesDbContext SecuritiesDb { get; private set; } = default!;
    public IEventSourceDbContext EventSourceDb { get; private set; } = default!;

    public SecuritiesFixture()
    {
        // Ensure runtime serializers are configured for tests
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
        
        SetDbFactory();
    }

    public IDataSerializer DataSerializer => ActorExtensions.DataSerializer!;
    public INatsSerializer<byte[]> MsgSerializer => ActorExtensions.MsgSerializer!;

    void SetDbFactory()
    {
        var dbConn = new DbConnectionSettings()
          .Add("SecuritiesDbConnection", "Contact Points=localhost;Port=9042;Username=ifmapp;Password=monkey35907;Default Keyspace=securities_test_db", "System.Data.ScyllaDb")
          .Add("EventSourceDbConnection", "Host=localhost;Port=5432;Username=postgres;Password=monkey35907;Database=event-source-test-db", "System.Data.Postgres");

        var diContainer = new Dictionary<Type, IObjectRepository>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var dbFactory = new DbContextFactory(dbResolver);

        var redisCache = Substitute.For<IRedisCache>();
        redisCache.When(_ => { }).Do(_ => { });
        var blackboardService = new BlackboardService(redisCache, new SystemTextJsonSerializer());

        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });

        var loggerDbEventSrc = Substitute.For<ILogger<DbProvider>>();
        loggerDbEventSrc.When(_ => { }).Do(_ => { });

        diContainer.Add(typeof(IObjectRepository<SecuritiesDbContext>), new SecuritiesDbContext(dbConn, dbFactory, logger));
        diContainer.Add(typeof(IObjectRepository<EventSourceDbContext>), new EventSourceDbContext(dbConn, dbFactory, blackboardService, loggerDbEventSrc));
        DbFactory = dbFactory;
        SecuritiesDb = (dbFactory.SecuritiesDb as ISecuritiesDbContext)!;
        EventSourceDb = (dbFactory.EventSourceDb as IEventSourceDbContext)!;
    }

    public TestableFuturesContractCommandActor CreateActor(
       IEventSourceActorDbContext? dbEventSource = null,
       ILogger<FuturesContractCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<FuturesContractCommandActor>>();
        return new TestableFuturesContractCommandActor(db, lg);
    }

    public TestableFuturesContractQueryActor CreateActor(
        IDbContextFactory dbFactory,
        ILogger<FuturesContractQueryActor>? logger = null)
    {
        var dbFact = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<FuturesContractQueryActor>>();
        return new TestableFuturesContractQueryActor(dbFact, lg);
    }

    public TestableFuturesOptionContractCommandActor CreateActor(
      IEventSourceActorDbContext? dbEventSource = null,
      ILogger<FuturesOptionContractCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<FuturesOptionContractCommandActor>>();
        return new TestableFuturesOptionContractCommandActor(db, lg);
    }

    public TestableFuturesOptionContractQueryActor CreateActor(
        IDbContextFactory dbFactory = null,
        ILogger<FuturesOptionContractQueryActor>? logger = null)
    {
        var dbFact = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<FuturesOptionContractQueryActor>>();
        return new TestableFuturesOptionContractQueryActor(dbFact, lg);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
