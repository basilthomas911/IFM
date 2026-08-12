using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Actor;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query.Actor;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using static TomasAI.IFM.Domain.MarketData.UnitTests.EconomicCalendar.EconomicCalendarCommandActorTests;
using static TomasAI.IFM.Domain.MarketData.UnitTests.EconomicCalendar.EconomicCalendarQueryActorTests;

namespace TomasAI.IFM.Domain.MarketData.UnitTests.EconomicCalendar;

public sealed class EconomicCalendarTestFixture : IDisposable
{
    public EconomicCalendarTestFixture()
    {
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
    }

    public TestableEconomicCalendarCommandActor CreateActor(
        IEventSourceActorDbContext? database = null,
        ILogger<EconomicCalendarCommandActor>? logger = null)
        => new(database ?? Substitute.For<IEventSourceActorDbContext>(),
            logger ?? Substitute.For<ILogger<EconomicCalendarCommandActor>>());

    public TestableEconomicCalendarQueryActor CreateActor(
        IDbContextFactory? databaseFactory = null,
        ILogger<EconomicCalendarQueryActor>? logger = null)
        => new(databaseFactory ?? Substitute.For<IDbContextFactory>(),
            logger ?? Substitute.For<ILogger<EconomicCalendarQueryActor>>());

    public void Dispose() { }
}
