using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.Command;
using TomasAI.IFM.Domain.SystemAdmin.Query;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

using static TomasAI.IFM.Domain.SystemAdmin.UnitTests.SystemAdminCommandActorTests;
using static TomasAI.IFM.Domain.SystemAdmin.UnitTests.SystemAdminQueryActorTests;
using TomasAI.IFM.Domain.SystemAdmin.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Query.Actor;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;

public class SystemAdminFixture : IDisposable
{
    public SystemAdminFixture()
    {
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
    }

    public IDataSerializer DataSerializer => ActorExtensions.DataSerializer!;
    public INatsSerializer<byte[]> MsgSerializer => ActorExtensions.MsgSerializer!;

    public TestableSystemAdminCommandActor CreateActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<SystemAdminCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<SystemAdminCommandActor>>();
        return new TestableSystemAdminCommandActor(db, lg);
    }

    public TestableSystemAdminQueryActor CreateQueryActor(
        ILogger<SystemAdminQueryActor>? logger = null)
    {
        var lg = logger ?? Substitute.For<ILogger<SystemAdminQueryActor>>();
        return new TestableSystemAdminQueryActor(lg);
    }

    public void Dispose()
    {
    }
}
