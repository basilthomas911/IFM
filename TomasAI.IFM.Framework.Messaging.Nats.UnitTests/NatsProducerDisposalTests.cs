using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class NatsProducerDisposalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposal_joins_concurrent_stops_and_late_supervisor_stops_without_allowing_restart(bool jetStream)
    {
        if (jetStream)
        {
            var producer = new NatsJetStreamActorProducer(new NatsJetStreamProducerOptions(), NullLogger<NatsJetStreamActorProducer>.Instance);
            await Task.WhenAll(producer.DisposeAsync().AsTask(), producer.DisposeAsync().AsTask(), producer.StopAsync().AsTask());
            await producer.StopAsync();
            Assert.False(producer.IsRunning);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => producer.StartAsync(new(ActorType.Event, "DisposedProducer")).AsTask());
        }
        else
        {
            var producer = new NatsActorProducer(new NatsProducerOptions(), NullLogger.Instance);
            await Task.WhenAll(producer.DisposeAsync().AsTask(), producer.DisposeAsync().AsTask(), producer.StopAsync().AsTask());
            await producer.StopAsync();
            Assert.False(producer.IsRunning);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => producer.StartAsync(new(ActorType.Command, "DisposedProducer")).AsTask());
        }
    }
}
