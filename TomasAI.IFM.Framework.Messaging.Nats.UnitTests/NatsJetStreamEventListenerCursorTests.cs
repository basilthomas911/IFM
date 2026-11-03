using System.Text.Json;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class NatsJetStreamEventListenerCursorTests
{
    [Theory]
    [InlineData(407236, 19490581, true)]
    [InlineData(0, 19490581, true)]
    [InlineData(407236, 407236, false)]
    [InlineData(407236, 400000, false)]
    [InlineData(0, 0, false)]
    public async Task Listener_repairs_rewound_stream_but_preserves_normal_purge_history(
        ulong lastSequence, ulong acknowledgementFloor, bool recreate)
    {
        await using var listener = new NatsJetStreamEventListener(new NatsJetStreamEventListenerOptions(), Substitute.For<ILogger>());
        var js = Substitute.For<INatsJSContext>();
        var stream = Substitute.For<INatsJSStream>();
        var old = Substitute.For<INatsJSConsumer>();
        var replacement = Substitute.For<INatsJSConsumer>();
        var config = new ConsumerConfig("scheduler-test") { FilterSubject = "Event.ScheduledTaskRuntime.>", AckPolicy = ConsumerConfigAckPolicy.Explicit, DeliverPolicy = ConsumerConfigDeliverPolicy.All };
        stream.Info.Returns(JsonSerializer.Deserialize<StreamInfo>(JsonSerializer.Serialize(
            new { config = new { name = "EventStream" }, state = new { messages = 0, first_seq = 1, last_seq = lastSequence } }))!);
        old.Info.Returns(JsonSerializer.Deserialize<ConsumerInfo>(JsonSerializer.Serialize(
            new { stream_name = "EventStream", name = "scheduler-test", ack_floor = new { stream_seq = acknowledgementFloor, consumer_seq = 1 } }))!);
        js.GetStreamAsync("EventStream", cancellationToken: Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(stream));
        js.CreateOrUpdateConsumerAsync("EventStream", config, Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(replacement));

        var result = await listener.RecoverInvalidCursorAsync(js, "EventStream", config, old, default);

        Assert.Same(recreate ? replacement : old, result);
        if (recreate)
        {
            await js.Received(1).DeleteConsumerAsync("EventStream", "scheduler-test", Arg.Any<CancellationToken>());
            await js.Received(1).CreateOrUpdateConsumerAsync("EventStream", config, Arg.Any<CancellationToken>());
        }
        else
        {
            await js.DidNotReceive().DeleteConsumerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            await js.DidNotReceive().CreateOrUpdateConsumerAsync(Arg.Any<string>(), Arg.Any<ConsumerConfig>(), Arg.Any<CancellationToken>());
        }
    }
}
