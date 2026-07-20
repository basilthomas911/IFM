using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Framework.Messaging.Nats.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Query;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Event;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using static TomasAI.IFM.Domain.OptionPricer.UnitTests.SpreadDistribution.SpreadDistributionCommandActorTests;
using static TomasAI.IFM.Domain.OptionPricer.UnitTests.SpreadDistribution.Job.SpreadDistributionJobCommandActorTests;

namespace TomasAI.IFM.Domain.OptionPricer.UnitTests;

public class OptionPricerTestFixture : IDisposable
{
    public OptionPricerTestFixture()
    {
        // Ensure runtime serializers are configured for tests
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
    }

    public IDataSerializer DataSerializer => ActorExtensions.DataSerializer!;
    public INatsSerializer<byte[]> MsgSerializer => ActorExtensions.MsgSerializer!;

    public TestableSpreadDistributionCommandActor CreateActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<SpreadDistributionCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        return new TestableSpreadDistributionCommandActor(db, lg);
    }

    public TestableSpreadDistributionJobCommandActor CreateJobCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<SpreadDistributionJobCommandActor>? logger = null)
    {
        var db = dbEventSource ?? Substitute.For<IEventSourceActorDbContext>();
        var lg = logger ?? Substitute.For<ILogger<SpreadDistributionJobCommandActor>>();
        return new TestableSpreadDistributionJobCommandActor(db, lg);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
