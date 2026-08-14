using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class NatsJetStreamActorConsumerTests
{
    [Theory]
    [InlineData("Event.Backup123.Execute")]
    [InlineData("Event.Backup123.Started")]
    [InlineData("Event.Backup123.Ignored")]
    public void TryParseActorSubject_RejectsLegacyThreeTokenBackupSubject(string subject)
    {
        var parsed = NatsJetStreamActorConsumer.TryParseActorSubject(subject, out _);

        Assert.False(parsed);
    }

    [Fact]
    public async Task TerminateMalformedSubjectAsync_TerminallyAcknowledgesPoisonDelivery()
    {
        var message = Substitute.For<INatsJSMsg<byte[]>>();
        message.AckTerminateAsync(Arg.Any<AckOpts?>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);
        var consumer = new NatsJetStreamActorConsumer(
            new NatsJetStreamConsumerOptions(),
            Substitute.For<ILogger>());

        await consumer.TerminateMalformedSubjectAsync(message, "Event.Backup123.Execute");

        await message.Received(1).AckTerminateAsync(
            Arg.Is<AckOpts?>(options => options.HasValue
                && options.Value.TerminateReason == "invalid-actor-subject"),
            CancellationToken.None);
    }
}
