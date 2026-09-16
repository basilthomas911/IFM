using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class NatsJetStreamActorConsumerTests
{
    [Theory]
    [InlineData(5, 2_294_812, 2_294_824, true)]
    [InlineData(5, 2_294_824, 2_294_824, false)]
    [InlineData(5, 2_294_825, 2_294_824, false)]
    [InlineData(0, 2_294_812, 2_294_824, false)]
    [InlineData(5, 0, 2_294_824, false)]
    public void RequiresCursorRecovery_OnlyAcceptsImpossibleNonEmptyStreamCursor(
        ulong retainedMessages,
        ulong lastStreamSequence,
        ulong acknowledgementFloorStreamSequence,
        bool expected)
    {
        var actual = NatsJetStreamActorConsumer.RequiresCursorRecovery(
            retainedMessages,
            lastStreamSequence,
            acknowledgementFloorStreamSequence);

        Assert.Equal(expected, actual);
    }

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
