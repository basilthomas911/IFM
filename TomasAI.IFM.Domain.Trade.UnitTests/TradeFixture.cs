using Microsoft.Extensions.Logging;
using NSubstitute;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Queries;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

using static TomasAI.IFM.Domain.Trade.UnitTests.Option.OptionTradeCommandActorTests;
using static TomasAI.IFM.Domain.Trade.UnitTests.Option.OptionTradeQueryActorTests;
using static TomasAI.IFM.Domain.Trade.UnitTests.Queries.TradeQueryActorTests;
using TomasAI.IFM.Domain.Trade.Option.Command;
using TomasAI.IFM.Domain.Trade.Option.Query;
using TomasAI.IFM.Domain.Trade.Option.Command.Actor;
using TomasAI.IFM.Domain.Trade.Option.Query.Actor;

namespace TomasAI.IFM.Domain.Trade.UnitTests;

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
