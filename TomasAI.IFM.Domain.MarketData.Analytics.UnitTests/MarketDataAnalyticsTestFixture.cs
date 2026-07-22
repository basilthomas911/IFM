using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Query;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Query;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Query;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Query;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Query;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesAdxSignal.FuturesAdxSignalCommandActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesAdxSignal.FuturesAdxSignalEventActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesAdxSignal.FuturesAdxSignalQueryActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesAtrSignal.FuturesAtrSignalCommandActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesAtrSignal.FuturesAtrSignalEventActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesAtrSignal.FuturesAtrSignalQueryActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal.FuturesItiSignalCommandActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal.FuturesItiSignalEventActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal.FuturesItiSignalQueryActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesMacdSignal.FuturesMacdSignalCommandActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesMacdSignal.FuturesMacdSignalEventActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesMacdSignal.FuturesMacdSignalQueryActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesRsiSignal.FuturesRsiSignalCommandActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesRsiSignal.FuturesRsiSignalEventActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesRsiSignal.FuturesRsiSignalQueryActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTdiSignal.FuturesTdiSignalCommandActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTdiSignal.FuturesTdiSignalEventActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTdiSignal.FuturesTdiSignalQueryActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTradeSignal.FuturesTradeSignalCommandActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTradeSignal.FuturesTradeSignalEventActorTests;
using static TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTradeSignal.FuturesTradeSignalQueryActorTests;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests;

public class MarketDataAnalyticsTestFixture : IDisposable
{
    public MarketDataAnalyticsTestFixture()
    {
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
    }

    public IDataSerializer DataSerializer => ActorExtensions.DataSerializer!;
    public INatsSerializer<byte[]> MsgSerializer => ActorExtensions.MsgSerializer!;

    public TestableFuturesItiSignalCommandActor CreateCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesItiSignalCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        return new TestableFuturesItiSignalCommandActor(db, lg);
    }

    public TestableFuturesItiSignalQueryActor CreateItiQueryActor(
        IDbContextFactory? dbFactory = null,
        ILogger<FuturesItiSignalQueryActor>? logger = null)
    {
        var db = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<FuturesItiSignalQueryActor>>();
        return new TestableFuturesItiSignalQueryActor(db, lg);
    }

    public TestableFuturesItiSignalEventActor CreateActor(
        IActorSupervisor? supervisor = null,
        ILogger<FuturesItiSignalEventActor>? logger = null)
    {
        var sv = supervisor ?? Substitute.For<IActorSupervisor>();
        var scw = Substitute.For<IStatusConsoleWriter>();
        var lg = logger ?? Substitute.For<ILogger<FuturesItiSignalEventActor>>();
        return new TestableFuturesItiSignalEventActor(sv, scw, lg);
    }

    public TestableFuturesRsiSignalCommandActor CreateRsiCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesRsiSignalCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<FuturesRsiSignalCommandActor>>();
        return new TestableFuturesRsiSignalCommandActor(db, lg);
    }

    public TestableFuturesRsiSignalQueryActor CreateRsiQueryActor(
        ILogger<FuturesRsiSignalQueryActor>? logger = null,
        IDbContextFactory? dbFactory = null)
    {
        var db = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<FuturesRsiSignalQueryActor>>();
        return new TestableFuturesRsiSignalQueryActor(db, lg);
    }

    public TestableFuturesRsiSignalEventActor CreateRsiEventActor(
        IActorSupervisor? supervisor = null,
        IStatusConsoleWriter statusConsoleWriter = null,
        ILogger<FuturesRsiSignalEventActor>? logger = null,
        IBlackboardService blackboardService = null)
    {
        var sv = supervisor ?? Substitute.For<IActorSupervisor>();
        var scw = statusConsoleWriter ?? Substitute.For<IStatusConsoleWriter>();
        var lg = logger ?? Substitute.For<ILogger<FuturesRsiSignalEventActor>>();
        var bb = blackboardService ?? Substitute.For<IBlackboardService>();
        return new TestableFuturesRsiSignalEventActor(sv, scw, lg, bb);
    }

    public TestableFuturesAtrSignalCommandActor CreateAtrCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesAtrSignalCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        return new TestableFuturesAtrSignalCommandActor(db, lg);
    }

    public TestableFuturesAtrSignalQueryActor CreateAtrQueryActor(
        ILogger<FuturesAtrSignalQueryActor>? logger = null,
        IDbContextFactory? dbFactory = null)
    {
        var db = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<FuturesAtrSignalQueryActor>>();
        return new TestableFuturesAtrSignalQueryActor(db, lg);
    }

    public TestableFuturesAtrSignalEventActor CreateAtrEventActor(
        IActorSupervisor? supervisor = null,
        ILogger<FuturesAtrSignalEventActor>? logger = null)
    {
        var sv = supervisor ?? Substitute.For<IActorSupervisor>();
        var scw = Substitute.For<IStatusConsoleWriter>();
        var lg = logger ?? Substitute.For<ILogger<FuturesAtrSignalEventActor>>();
        return new TestableFuturesAtrSignalEventActor(sv, scw, lg);
    }

    public TestableFuturesMacdSignalCommandActor CreateMacdCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        IDbContextFactory dbFactory = null,
        ILogger<FuturesMacdSignalCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();

        var lg = logger ?? Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var dbFact = dbFactory ?? Substitute.For<IDbContextFactory>();
        return new TestableFuturesMacdSignalCommandActor(db, dbFact, lg);
    }

    // Overload to preserve existing test call sites that provide (IEventSourceActorDbContext, ILogger)
    public TestableFuturesMacdSignalCommandActor CreateMacdCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesMacdSignalCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var dbFact = Substitute.For<IDbContextFactory>();
        return new TestableFuturesMacdSignalCommandActor(db, dbFact, lg);
    }

    public TestableFuturesMacdSignalQueryActor CreateMacdQueryActor(
        ILogger<FuturesMacdSignalQueryActor>? logger = null,
        IDbContextFactory? dbFactory = null)
    {
        var db = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<FuturesMacdSignalQueryActor>>();
        return new TestableFuturesMacdSignalQueryActor(db, lg);
    }

    public TestableFuturesMacdSignalEventActor CreateMacdEventActor(
        IActorSupervisor? supervisor = null,
        IStatusConsoleWriter statusConsoleWriter = null,
        ILogger<FuturesMacdSignalEventActor>? logger = null)
    {
        var sv = supervisor ?? Substitute.For<IActorSupervisor>();
        var scw = statusConsoleWriter ?? Substitute.For<IStatusConsoleWriter>();
        var lg = logger ?? Substitute.For<ILogger<FuturesMacdSignalEventActor>>();
        return new TestableFuturesMacdSignalEventActor(sv, scw, lg);
    }

    public TestableFuturesTdiSignalCommandActor CreateTdiCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesTdiSignalCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        return new TestableFuturesTdiSignalCommandActor(db, lg);
    }

    public TestableFuturesTdiSignalCommandActor CreateActor(
        IEventSourceActorDbContext dbEventSource,
        ILogger<FuturesTdiSignalCommandActor> logger)
        => new(dbEventSource, logger);

    public TestableFuturesTdiSignalQueryActor CreateTdiQueryActor(
        ILogger<FuturesTdiSignalQueryActor>? logger = null,
        IDbContextFactory? dbFactory = null)
    {
        var db = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        return new TestableFuturesTdiSignalQueryActor(db, lg);
    }

    public TestableFuturesTdiSignalEventActor CreateTdiEventActor(
        IActorSupervisor? supervisor = null,
        ILogger<FuturesTdiSignalEventActor>? logger = null,
        IStatusConsoleWriter statucConsoleWriter = null)
    {
        var sv = supervisor ?? Substitute.For<IActorSupervisor>();
        var lg = logger ?? Substitute.For<ILogger<FuturesTdiSignalEventActor>>();
        var scw = statucConsoleWriter ?? Substitute.For<IStatusConsoleWriter>();
        return new TestableFuturesTdiSignalEventActor(sv, scw, lg);
    }

    public TestableFuturesTradeSignalCommandActor CreateTradeSignalCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesTradeSignalCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        return new TestableFuturesTradeSignalCommandActor(db, lg);
    }

    public TestableFuturesTradeSignalQueryActor CreateTradeSignalQueryActor(
        ILogger<FuturesTradeSignalQueryActor>? logger = null,
        IDbContextFactory? dbFactory = null)
    {
        var db = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<FuturesTradeSignalQueryActor>>();
        return new TestableFuturesTradeSignalQueryActor(db, lg);
    }

    public TestableFuturesTradeSignalEventActor CreateTradeSignalEventActor(
        IActorSupervisor? supervisor = null,
        ILogger<FuturesTradeSignalEventActor>? logger = null,
        IStatusConsoleWriter statusConsoleWriter = null)
    {
        var sv = supervisor ?? Substitute.For<IActorSupervisor>();
        var lg = logger ?? Substitute.For<ILogger<FuturesTradeSignalEventActor>>();
        var scw = statusConsoleWriter ?? Substitute.For<IStatusConsoleWriter>();
        return new TestableFuturesTradeSignalEventActor(sv, scw, lg);
    }

    public TestableFuturesAdxSignalCommandActor CreateAdxCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesAdxSignalCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<FuturesAdxSignalCommandActor>>();
        return new TestableFuturesAdxSignalCommandActor(db, lg);
    }

    public TestableFuturesAdxSignalQueryActor CreateAdxQueryActor(
        IDbContextFactory? dbFactory = null,
        ILogger<FuturesAdxSignalQueryActor>? logger = null)
    {
        var db = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<FuturesAdxSignalQueryActor>>();
        return new TestableFuturesAdxSignalQueryActor(db, lg);
    }

    public TestableFuturesAdxSignalEventActor CreateAdxEventActor(
        IActorSupervisor? supervisor = null,
        ILogger<FuturesAdxSignalEventActor>? logger = null)
    {
        var sv = supervisor ?? Substitute.For<IActorSupervisor>();
        var lg = logger ?? Substitute.For<ILogger<FuturesAdxSignalEventActor>>();
        var scw =  Substitute.For<IStatusConsoleWriter>();    
        return new TestableFuturesAdxSignalEventActor(sv, scw, lg);
    }

    public void Dispose() { }
}
