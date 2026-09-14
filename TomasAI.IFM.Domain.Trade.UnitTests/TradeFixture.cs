using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Queries;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using static TomasAI.IFM.Domain.Trade.UnitTests.Queries.TradeQueryActorTests;

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

    public TestableTradeQueryActor CreateTradeQueryActor(
        IDbContextFactory? dbFactory = null,
        ILogger<TradeQueryActor>? logger = null)
    {
        var db = dbFactory ?? Substitute.For<IDbContextFactory>();
        var log = logger ?? Substitute.For<ILogger<TradeQueryActor>>();
        return new TestableTradeQueryActor(db, log);
    }

    public void Dispose() { }
}
