using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class NatsConsumerDisposalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public async Task Disposal_and_late_supervisor_stops_join_without_disposing_the_lifecycle_gate(bool jetStream)
    {
        if(jetStream)
        {
            var consumer=new NatsJetStreamActorConsumer(new NatsJetStreamConsumerOptions(),NullLogger.Instance);
            await Task.WhenAll(consumer.DisposeAsync().AsTask(),consumer.DisposeAsync().AsTask(),consumer.StopAsync().AsTask());
            await consumer.StopAsync();
        }
        else
        {
            var consumer=new NatsActorConsumer(new NatsConsumerOptions(),NullLogger.Instance);
            await Task.WhenAll(consumer.DisposeAsync().AsTask(),consumer.DisposeAsync().AsTask(),consumer.StopAsync().AsTask());
            await consumer.StopAsync();
        }
    }
}
