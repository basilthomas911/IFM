using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Framework.Storage;

using TomasAI.IFM.Framework.Messaging.Nats.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using static TomasAI.IFM.Domain.Fund.UnitTests.FundCommandActorTests;
using static TomasAI.IFM.Domain.Fund.UnitTests.FundEventActorTests;
using static TomasAI.IFM.Domain.Fund.UnitTests.FundQueryActorTests;
using static TomasAI.IFM.Domain.Fund.UnitTests.Transaction.FundTransactionCommandActorTests;
using static TomasAI.IFM.Domain.Fund.UnitTests.Transaction.FundTransactionQueryActorTests;
using static TomasAI.IFM.Domain.Fund.UnitTests.Transaction.FundTransactionEventActorTests;
using TomasAI.IFM.Domain.Fund.Transaction.Query;
using TomasAI.IFM.Domain.Fund.Transaction.Event;
using TomasAI.IFM.Domain.Fund.Query.Actor;
using TomasAI.IFM.Domain.Fund.Event.Actor;
using TomasAI.IFM.Domain.Fund.Command.Actor;
using TomasAI.IFM.Domain.Fund.Transaction.Command.Actor;

namespace TomasAI.IFM.Domain.Fund.UnitTests;

/// <summary>
/// Provides a test fixture for fund-related actor tests, supplying configured serializers and factory methods for
/// creating testable actor instances.
/// </summary>
/// <remarks>This fixture ensures that required serializers are initialized for test scenarios and offers
/// convenient methods to instantiate fund command, event, query, and denormalizer actors with optional dependencies. It
/// is intended for use in unit and integration tests to streamline setup and dependency management.</remarks>
public class FundTestFixture : IDisposable
{
    public FundTestFixture()
    {
        // Ensure runtime serializers are configured for tests
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
    }

    public IDataSerializer DataSerializer => ActorExtensions.DataSerializer!;
    public INatsSerializer<byte[]> MsgSerializer => ActorExtensions.MsgSerializer!;

    public TestableFundCommandActor CreateActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FundCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<FundCommandActor>>();
        return new TestableFundCommandActor(db, lg);
    }

    public TestableFundEventActor CreateActor(
       IActorSupervisor? supervisor = null,
       ILogger<FundEventActor>? logger = null)
    {
        var su = supervisor ?? Substitute.For<IActorSupervisor>();
        var lg = logger ?? Substitute.For<ILogger<FundEventActor>>();
        return new TestableFundEventActor(su, lg);
    }

    public TestableFundQueryActor CreateActor(
                IDbContextFactory dbFactor,
      ILogger<FundQueryActor>? logger = null)
    {
        var  dbContextResolver = Substitute.For<IDbContextResolver>();
                 dbContextResolver.Resolve<FundDbContext>().Returns(Substitute.For<IObjectRepository<FundDbContext>>());
                var db = dbFactor ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<FundQueryActor>>();
        return new TestableFundQueryActor(db,lg);
    }

    public TestableFundTransactionCommandActor CreateActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FundTransactionCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<FundTransactionCommandActor>>();
        return new TestableFundTransactionCommandActor(db, lg);
    }

    public TestableFundTransactionQueryActor CreateActor(
        IDbContextFactory dbFactory,
        ILogger<FundTransactionQueryActor>? logger = null)
    {
        var db = dbFactory ?? Substitute.For<IDbContextFactory>();
        var lg = logger ?? Substitute.For<ILogger<FundTransactionQueryActor>>();
        return new TestableFundTransactionQueryActor(db, lg);
    }

    public TestableFundTransactionEventActor CreateActor(
        IActorSupervisor supervisor,
        ILogger<FundTransactionEventActor>? logger = null)
    {
        var su = supervisor ?? Substitute.For<IActorSupervisor>();
        var lg = logger ?? Substitute.For<ILogger<FundTransactionEventActor>>();
        return new TestableFundTransactionEventActor(su, lg);
    }

    public void Dispose() { }
}
