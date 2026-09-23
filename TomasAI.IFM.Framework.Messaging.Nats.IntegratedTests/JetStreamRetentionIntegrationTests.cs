using FluentAssertions;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;

namespace TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests;

[Trait("Category", "Integration")]
public sealed class JetStreamRetentionIntegrationTests
{
    [Fact]
    public async Task Interest_stream_removes_message_only_after_both_consumers_acknowledge()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var streamName = $"IFM_RETENTION_TEST_{suffix}";
        var subject = $"ifm.retention.test.{suffix}";
        var url = Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";
        await using var client = new NatsClient(url);
        await client.ConnectAsync();
        var jetStream = client.CreateJetStreamContext();
        try
        {
            var stream = await jetStream.CreateStreamAsync(new StreamConfig(streamName, [subject])
            {
                Retention = StreamConfigRetention.Interest
            });
            var first = await stream.CreateOrUpdateConsumerAsync(new ConsumerConfig("first")
            {
                FilterSubject = subject,
                AckPolicy = ConsumerConfigAckPolicy.Explicit
            });
            var second = await stream.CreateOrUpdateConsumerAsync(new ConsumerConfig("second")
            {
                FilterSubject = subject,
                AckPolicy = ConsumerConfigAckPolicy.Explicit
            });
            var acknowledgement = await jetStream.PublishAsync(subject, new byte[] { 1 },
                serializer: new NatsByteArrayMessageSerializer());
            acknowledgement.EnsureSuccess();
            (await jetStream.GetStreamAsync(streamName)).Info.State.Messages.Should().Be(1);

            await AcknowledgeOneAsync(first);
            (await jetStream.GetStreamAsync(streamName)).Info.State.Messages.Should().Be(1);

            await AcknowledgeOneAsync(second);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while ((await jetStream.GetStreamAsync(streamName)).Info.State.Messages != 0)
                await Task.Delay(25, timeout.Token);
        }
        finally
        {
            await jetStream.DeleteStreamAsync(streamName);
        }
    }

    private static async Task AcknowledgeOneAsync(INatsJSConsumer consumer)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var message in consumer.ConsumeAsync<byte[]>(
            serializer: new NatsByteArrayMessageSerializer(),
            cancellationToken: timeout.Token))
        {
            await message.AckAsync(cancellationToken: timeout.Token);
            break;
        }
    }
}
