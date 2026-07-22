using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Feed.Command;
using TomasAI.IFM.Domain.MarketData.Feed.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Query;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Query;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query;
using TomasAI.IFM.Domain.MarketData.Feed.Query;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.Trade.Contracts;
using static TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesBarData.FuturesBarDataCommandActorTests;
using static TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesBarData.FuturesBarDataEventActorTests;
using static TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesBarData.FuturesBarDataQueryActorTests;
using static TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesClosingPrice.FuturesClosingPriceCommandActorTests;
using static TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesEodData.FuturesEodDataCommandActorTests;
using static TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesEodData.FuturesEodDataEventActorTests;
using static TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesEodData.FuturesEodDataQueryActorTests;
using static TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesOptionQuoteData.FuturesOptionQuoteDataCommandActorTests;
using static TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesOptionQuoteData.FuturesOptionQuoteDataEventActorTests;
using static TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesOptionTickData.FuturesOptionTickDataCommandActorTests;
using static TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesOptionTickData.FuturesOptionTickDataEventActorTests;
using static TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesOptionTickData.FuturesOptionTickDataQueryActorTests;
using static TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesTickData.FuturesTickDataCommandActorTests;
using static TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesTickData.FuturesTickDataEventActorTests;
using static TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesTickData.FuturesTickDataQueryActorTests;
using static TomasAI.IFM.Domain.MarketData.Feed.UnitTests.MarketDataFeed.MarketDataFeedCommandActorTests;
using static TomasAI.IFM.Domain.MarketData.Feed.UnitTests.MarketDataFeed.MarketDataFeedEventActorTests;
using static TomasAI.IFM.Domain.MarketData.Feed.UnitTests.MarketDataFeed.MarketDataFeedQueryActorTests;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Actor;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests;

/// <summary>
/// Provides a test fixture for market data actor tests, supplying configured serializers and factory methods for
/// creating testable actor instances.
/// </summary>
public class MarketDataFeedTestFixture : IDisposable
{
    public MarketDataFeedTestFixture()
    {
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
    }

    public IDataSerializer DataSerializer => ActorExtensions.DataSerializer!;
    public INatsSerializer<byte[]> MsgSerializer => ActorExtensions.MsgSerializer!;

    public TestableMarketDataFeedCommandActor CreateActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<MarketDataFeedCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<MarketDataFeedCommandActor>>();
        return new TestableMarketDataFeedCommandActor(db, lg);
    }

    /*
    public TestableMarketDataFeedQueryActor CreateQueryActor(
        ILogger<MarketDataFeedQueryActor>? logger = null,
        IDbContextFactory? dbFactory = null)
    {
        var db = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<MarketDataFeedQueryActor>>();
        return new TestableMarketDataFeedQueryActor(db, lg);
    }
    */

    public TestableFuturesBarDataCommandActor CreateActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesBarDataCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        return new TestableFuturesBarDataCommandActor(db, lg);
    }

    public TestableFuturesBarDataQueryActor CreateFuturesBarDataQueryActor(
        ILogger<FuturesBarDataQueryActor>? logger = null,
        IDbContextFactory? dbFactory = null)
    {
        var db = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<FuturesBarDataQueryActor>>();
        return new TestableFuturesBarDataQueryActor(db, lg);
    }

    public TestableFuturesBarDataQueryActor CreateActor(
        IDbContextFactory dbFactory,
        ILogger<FuturesBarDataQueryActor> logger)
        => new(dbFactory, logger);

    public TestableFuturesBarDataEventActor CreateFuturesBarDataEventActor(
        IActorSupervisor? supervisor = null,
        IFuturesBarDataTimer? futuresBarTimer = null,
        IStatusConsoleWriter? statusConsoleWriter = null,
        ILogger<FuturesBarDataEventActor>? logger = null)
    {
        var su = supervisor ?? Substitute.For<IActorSupervisor>();
        var fbt = futuresBarTimer ?? Substitute.For<IFuturesBarDataTimer>();
        var scw = statusConsoleWriter ?? Substitute.For<IStatusConsoleWriter>();
        var lg = logger ?? Substitute.For<ILogger<FuturesBarDataEventActor>>();
        return new TestableFuturesBarDataEventActor(su, fbt, scw, lg);
    }

    public TestableFuturesBarDataEventActor CreateActor(
        IActorSupervisor supervisor,
        IFuturesBarDataTimer futuresBarTimer,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesBarDataEventActor> logger)
        => new(supervisor, futuresBarTimer, statusConsoleWriter, logger);

    public TestableFuturesClosingPriceCommandActor CreateFuturesClosingPriceCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesClosingPriceCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        return new TestableFuturesClosingPriceCommandActor(db, lg);
    }

    public TestableFuturesClosingPriceCommandActor CreateActor<TActor>(
        IEventSourceActorDbContext dbEventSource,
        ILogger<FuturesClosingPriceCommandActor> logger)
        where TActor : FuturesClosingPriceCommandActor
        => new(dbEventSource, logger);

    public TestableFuturesOptionQuoteDataCommandActor CreateFuturesOptionQuoteDataCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesOptionQuoteDataCommandActor>? logger = null,
        IReferenceLookupService? refLookupService = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var refLookup = refLookupService ?? Substitute.For<IReferenceLookupService>();
        return new TestableFuturesOptionQuoteDataCommandActor(refLookup, db, lg);
    }

    public TestableFuturesOptionQuoteDataCommandActor CreateActor(
        IEventSourceActorDbContext dbEventSource,
        ILogger<FuturesOptionQuoteDataCommandActor> logger,
        IReferenceLookupService? refLookupService = null)
        => new(refLookupService ?? Substitute.For<IReferenceLookupService>(), dbEventSource, logger);

    public TestableFuturesOptionQuoteDataEventActor CreateFuturesOptionQuoteDataEventActor(
        IActorSupervisor? supervisor = null,
        IMarketDataSnapshotApi marketDataSnapshotApi =  null,
        IBlackboardService blackboardService = null,
        IStatusConsoleWriter statusConsoleWriter = null,    
        ILogger<FuturesOptionQuoteDataEventActor>? logger = null)
    {
        var su = supervisor ?? Substitute.For<IActorSupervisor>();
        var mds = marketDataSnapshotApi ?? Substitute.For<IMarketDataSnapshotApi>();
        var bb = blackboardService ?? Substitute.For<IBlackboardService>();
        var scw = statusConsoleWriter ?? Substitute.For<IStatusConsoleWriter>();
        var lg = logger ?? Substitute.For<ILogger<FuturesOptionQuoteDataEventActor>>();
        return new TestableFuturesOptionQuoteDataEventActor(su, mds, bb,scw, lg);
    }

    public TestableFuturesOptionQuoteDataEventActor CreateActor(
        IActorSupervisor supervisor,
        IMarketDataSnapshotApi marketDataSnapshotApi,
        IBlackboardService blackboardService,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesOptionQuoteDataEventActor> logger)
        => new(supervisor, marketDataSnapshotApi, blackboardService, statusConsoleWriter, logger);

    public TestableMarketDataFeedEventActor CreateMarketDataFeedEventActor(
        IActorSupervisor? supervisor = null,
        IMarketDataApi marketDataApi = null,
        IOptionTradeLiveFeedMap optionTradeLiveFeedMap = null,
        IBlackboardService blackboardService = null,
        IStatusConsoleWriter statusConsoleWriter = null,
        ILogger<MarketDataFeedEventActor>? logger = null)
    {
        var su = supervisor ?? Substitute.For<IActorSupervisor>();
        var mda = marketDataApi ?? Substitute.For<IMarketDataApi>();
        var lfm = optionTradeLiveFeedMap ?? Substitute.For<IOptionTradeLiveFeedMap>();
        var bb = blackboardService ?? Substitute.For<IBlackboardService>();
        var scw = statusConsoleWriter ?? Substitute.For<IStatusConsoleWriter>();
        var lg = logger ?? Substitute.For<ILogger<MarketDataFeedEventActor>>();
        return new TestableMarketDataFeedEventActor(su, mda, lfm, bb, scw, lg);
    }

    public TestableFuturesOptionTickDataCommandActor CreateFuturesOptionTickDataCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesOptionTickDataCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        return new TestableFuturesOptionTickDataCommandActor(db, lg);
    }

    public TestableFuturesOptionTickDataCommandActor CreateActor(
        IEventSourceActorDbContext dbEventSource,
        ILogger<FuturesOptionTickDataCommandActor> logger)
        => new(dbEventSource, logger);

    public TestableFuturesOptionTickDataQueryActor CreateFuturesOptionTickDataQueryActor(
        ILogger<FuturesOptionTickDataQueryActor>? logger = null,
        IDbContextFactory? dbFactory = null)
    {
        var db = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        return new TestableFuturesOptionTickDataQueryActor(db, lg);
    }

    public TestableFuturesOptionTickDataQueryActor CreateActor(
        ILogger<FuturesOptionTickDataQueryActor> logger,
        IDbContextFactory? dbFactory = null)
        => new(dbFactory ?? Substitute.For<IDbContextFactory>(), logger);

    public TestableFuturesOptionTickDataEventActor CreateFuturesOptionTickDataEventActor(
        IActorSupervisor? supervisor = null,
        IMarketDataApi marketDataApi = null,
        IMarketDataSnapshotApi marketDataSnapshotApi = null,
        IBlackboardService blackboardService = null,
        IOptionTradeLiveFeedMap optionTradeLiveFeedMap = null,
        IStatusConsoleWriter statusConsoleWriter = null,
        ILogger<FuturesOptionTickDataEventActor>? logger = null)
    {
        var su = supervisor ?? Substitute.For<IActorSupervisor>();
        var mda = marketDataApi ?? Substitute.For<IMarketDataApi>();
        var mdsa = marketDataSnapshotApi ?? Substitute.For<IMarketDataSnapshotApi>();
        var bb = blackboardService ?? Substitute.For<IBlackboardService>();
        var fa = optionTradeLiveFeedMap ?? Substitute.For<IOptionTradeLiveFeedMap>();
        var scw = statusConsoleWriter ?? Substitute.For<IStatusConsoleWriter>();
        var lg = logger ?? Substitute.For<ILogger<FuturesOptionTickDataEventActor>>();
        return new TestableFuturesOptionTickDataEventActor(su, mda, mdsa, bb, fa, scw, lg);
    }

    public TestableFuturesOptionTickDataEventActor CreateActor(
        IActorSupervisor supervisor,
        IMarketDataApi marketDataApi,
        IMarketDataSnapshotApi marketDataSnapshotApi,
        IBlackboardService blackboardService,
        IOptionTradeLiveFeedMap optionTradeLiveFeedMap,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesOptionTickDataEventActor> logger)
        => new(supervisor, marketDataApi, marketDataSnapshotApi, blackboardService,
            optionTradeLiveFeedMap, statusConsoleWriter, logger);

    public TestableFuturesTickDataCommandActor CreateFuturesTickDataCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesTickDataCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<FuturesTickDataCommandActor>>();
        return new TestableFuturesTickDataCommandActor(db, lg);
    }

    public TestableFuturesTickDataCommandActor CreateActor(
        IEventSourceActorDbContext dbEventSource,
        ILogger<FuturesTickDataCommandActor> logger)
        => new(dbEventSource, logger);

    public TestableFuturesTickDataQueryActor CreateFuturesTickDataQueryActor(
        ILogger<FuturesTickDataQueryActor>? logger = null,
        IDbContextFactory? dbFactory = null)
    {
        var db = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<FuturesTickDataQueryActor>>();
        return new TestableFuturesTickDataQueryActor(db, lg);
    }

    public TestableFuturesTickDataQueryActor CreateActor(
        IDbContextFactory dbFactory,
        ILogger<FuturesTickDataQueryActor> logger)
        => new(dbFactory, logger);

    public TestableFuturesTickDataEventActor CreateFuturesTickDataEventActor(
        IActorSupervisor? supervisor = null,
        IMarketDataApi marketDataApi = null,
        IBlackboardService blackboardService = null,
        IStatusConsoleWriter statusConsoleWriter = null,
        ILogger<FuturesTickDataEventActor>? logger = null)
    {
        var su = supervisor ?? Substitute.For<IActorSupervisor>();
        var mda  = marketDataApi ?? Substitute.For<IMarketDataApi>();
        var bb = blackboardService ?? Substitute.For<IBlackboardService>();
        var scw = statusConsoleWriter ?? Substitute.For<IStatusConsoleWriter>();
        var lg = logger ?? Substitute.For<ILogger<FuturesTickDataEventActor>>();
        return new TestableFuturesTickDataEventActor(su, mda, bb, scw, lg);
    }

    public TestableFuturesTickDataEventActor CreateActor(
        IActorSupervisor supervisor,
        IMarketDataApi marketDataApi,
        IBlackboardService blackboardService,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesTickDataEventActor> logger)
        => new(supervisor, marketDataApi, blackboardService, statusConsoleWriter, logger);

    public TestableFuturesEodDataCommandActor CreateActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesEodDataCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        return new TestableFuturesEodDataCommandActor(db, lg);
    }

    public TestableFuturesEodDataQueryActor CreateActor(
        IDbContextFactory dbFactory,
        ILogger<FuturesEodDataQueryActor> logger)
    {
        return new TestableFuturesEodDataQueryActor(dbFactory, logger);
    }

    public TestableFuturesEodDataEventActor CreateActor(
        IActorSupervisor supervisor,
        IBlackboardService blackboardService,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesEodDataEventActor> logger)
    {
        return new TestableFuturesEodDataEventActor(
            supervisor, blackboardService, statusConsoleWriter, logger);
    }

    public void Dispose() { }
}
