using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Query;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using static TomasAI.IFM.Domain.MarketData.UnitTests.MarketDataQueryActorTests;
using TomasAI.IFM.Domain.MarketData.Query.Actor;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

/// <summary>
/// Provides a test fixture for market data actor tests, supplying configured serializers and factory methods for
/// creating testable actor instances.
/// </summary>
public class MarketDataTestFixture : IDisposable
{
    public MarketDataTestFixture()
    {
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
    }

    public IDataSerializer DataSerializer => ActorExtensions.DataSerializer!;

    public TestableMarketDataQueryActor CreateActor(
        ILogger<MarketDataQueryActor>? logger = null,
        IDbContextFactory? dbFactory = null)
    {
        var db = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<MarketDataQueryActor>>();
        return new TestableMarketDataQueryActor(db, lg);
    }

    public void Dispose() { }
}
