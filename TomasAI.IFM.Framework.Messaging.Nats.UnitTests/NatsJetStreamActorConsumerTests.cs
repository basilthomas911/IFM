using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class NatsJetStreamActorConsumerTests
{
    [Theory]
    [InlineData(2_294_812, 2_294_824, true)]
    [InlineData(2_294_824, 2_294_824, false)]
    [InlineData(2_294_825, 2_294_824, false)]
    // The observed startup failure: purge emptied the rebuilt stream before consumer startup.
    [InlineData(2583, 19_499_427, true)]
    [InlineData(0, 2_294_824, true)]
    [InlineData(0, 0, false)]
    // A normal purge keeps the last sequence and must preserve the valid consumer.
    [InlineData(2583, 2583, false)]
    [InlineData(2583, 2500, false)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public void RequiresCursorRecovery_RejectsImpossibleCursorEvenWhenStreamIsEmpty(
        ulong lastStreamSequence,
        ulong acknowledgementFloorStreamSequence,
        bool expected)
    {
        var actual = NatsJetStreamActorConsumer.RequiresCursorRecovery(
            lastStreamSequence,
            acknowledgementFloorStreamSequence);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Event.Backup123.Execute")]
    [InlineData("Event.Backup123.Started")]
    [InlineData("Event.Backup123.Ignored")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public void TryParseActorSubject_RejectsLegacyThreeTokenBackupSubject(string subject)
    {
        var parsed = NatsJetStreamActorConsumer.TryParseActorSubject(subject, out _);

        Assert.False(parsed);
    }

    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2000",Justification="The test deliberately exercises transferred resource ownership and explicit disposal assertions.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2012",Justification="This ValueTask invocation configures or verifies an NSubstitute call, rather than executing asynchronous work.")]
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
