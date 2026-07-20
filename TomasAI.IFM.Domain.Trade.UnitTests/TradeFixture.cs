using Microsoft.Extensions.Logging;
using NSubstitute;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Actor.Queries;
using TomasAI.IFM.Framework.Messaging.Nats.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

using static TomasAI.IFM.Domain.Trade.Actor.UnitTests.Option.OptionTradeCommandActorTests;
using static TomasAI.IFM.Domain.Trade.Actor.UnitTests.Option.OptionTradeQueryActorTests;
using static TomasAI.IFM.Domain.Trade.Actor.UnitTests.Queries.TradeQueryActorTests;
using TomasAI.IFM.Domain.Trade.Actor.Option.Command;
using TomasAI.IFM.Domain.Trade.Actor.Option.Query;

namespace TomasAI.IFM.Domain.Trade.Actor.UnitTests;

public class TradeFixture : IDisposable
{
    public TradeFixture()
    {
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
    }

    public IDataSerializer DataSerializer => ActorExtensions.DataSerializer!;
    public INatsSerializer<byte[]> MsgSerializer => ActorExtensions.MsgSerializer!;

    public TestableOptionTradeCommandActor CreateActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<OptionTradeCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<OptionTradeCommandActor>>();
        return new TestableOptionTradeCommandActor(db, lg);
    }

    public TestableOptionTradeQueryActor CreateQueryActor(
        IDbContextFactory? dbFactory = null,
        ILogger<OptionTradeQueryActor>? logger = null)
    {
        var db = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<OptionTradeQueryActor>>();
        return new TestableOptionTradeQueryActor(db, lg);
    }


    public TestableTradeQueryActor CreateTradeQueryActor(
        IDbContextFactory? dbFactory = null,
        ILogger<TradeQueryActor>? logger = null)
    {
        var db = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<TradeQueryActor>>();
        return new TestableTradeQueryActor(db, lg);
    }

    public void Dispose() { }
}
