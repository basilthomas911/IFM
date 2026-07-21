using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.Reference.EconomicCalendar.Command;
using TomasAI.IFM.Domain.Reference.EconomicCalendar.Query;
using TomasAI.IFM.Domain.Reference.LookupType.Command;
using TomasAI.IFM.Domain.Reference.LookupType.Query;
using TomasAI.IFM.Domain.Reference.Query;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using static TomasAI.IFM.Domain.Reference.UnitTests.EconomicCalendar.EconomicCalendarCommandActorTests;
using static TomasAI.IFM.Domain.Reference.UnitTests.EconomicCalendar.EconomicCalendarQueryActorTests;
using static TomasAI.IFM.Domain.Reference.UnitTests.LookupType.LookupTypeCommandActorTests;
using static TomasAI.IFM.Domain.Reference.UnitTests.LookupType.LookupTypeQueryActorTests;
using static TomasAI.IFM.Domain.Reference.UnitTests.Queries.ReferenceQueryActorTests;
using TomasAI.IFM.Domain.Reference.EconomicCalendar.Command.Actor;
using TomasAI.IFM.Domain.Reference.EconomicCalendar.Query.Actor;
using TomasAI.IFM.Domain.Reference.LookupType.Command.Actor;
using TomasAI.IFM.Domain.Reference.LookupType.Query.Actor;
using TomasAI.IFM.Domain.Reference.Query.Actor;

namespace TomasAI.IFM.Domain.Reference.UnitTests;

/// <summary>
/// Provides a test fixture for reference-related actor tests, supplying configured serializers and factory methods for
/// creating testable actor instances.
/// </summary>
/// <remarks>This fixture ensures that required serializers are initialized for test scenarios and offers
/// convenient methods to instantiate lookup type and economic calendar command, event, query, and denormalizer actors with optional dependencies. It
/// is intended for use in unit and integration tests to streamline setup and dependency management.</remarks>
public class ReferenceTestFixture : IDisposable
{
    public ReferenceTestFixture()
    {
        // Ensure runtime serializers are configured for tests
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
    }

    public IDataSerializer DataSerializer => ActorExtensions.DataSerializer!;
    public INatsSerializer<byte[]> MsgSerializer => ActorExtensions.MsgSerializer!;


    public TestableEconomicCalendarCommandActor CreateActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<EconomicCalendarCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<EconomicCalendarCommandActor>>();
        return new TestableEconomicCalendarCommandActor(db, lg);
    }

    public TestableEconomicCalendarQueryActor CreateActor(
        IDbContextFactory dbFactory = null,
        ILogger<EconomicCalendarQueryActor>? logger = null)
    {
        var dbFact = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<EconomicCalendarQueryActor>>();
        return new TestableEconomicCalendarQueryActor(dbFact,lg);
    }

    public TestableLookupTypeCommandActor CreateActor(
    IEventSourceActorDbContext? dbEventSource = null,
    ILogger<LookupTypeCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<LookupTypeCommandActor>>();
        return new TestableLookupTypeCommandActor(db, lg);
    }

    public TestableLookupTypeQueryActor CreateActor(
        IDbContextFactory dbFactory = null,
        ILogger<LookupTypeQueryActor>? logger = null)
    {
        var dbFact = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<LookupTypeQueryActor>>();
        return new TestableLookupTypeQueryActor(dbFact, lg);
    }

    public TestableReferenceQueryActor CreateActor(
        IDbContextFactory dbFactory = null,
        ILogger<ReferenceQueryActor>? logger = null)
    {
        var dbFact = Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<ReferenceQueryActor>>();
        return new TestableReferenceQueryActor(dbFact, lg);
    }

    public void Dispose() { }
}
