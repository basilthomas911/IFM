using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class NatsProtectedRealtimeDeliveryTests
{
    static readonly ActorSubject Subject = new(ActorType.Realtime, "FuturesTradeSessionBarSignal", "Updated", "ES");

    [Theory]
    [InlineData(ActorAdmissionReason.GlobalMessageLimit)]
    [InlineData(ActorAdmissionReason.GlobalByteLimit)]
    [InlineData(ActorAdmissionReason.ActorTypeMessageLimit)]
    [InlineData(ActorAdmissionReason.ActorTypeByteLimit)]
    [InlineData(ActorAdmissionReason.MailboxLimit)]
    public async Task Capacity_pressure_retries_the_same_owned_input_until_admitted(ActorAdmissionReason reason)
    {
        var (supervisor, queues, _) = Fixture();
        var message = Substitute.For<IActorMessage>();
        var attempts = 0;
        queues.TryAdmitAsync(message, Subject, Arg.Any<CancellationToken>()).Returns(_ =>
            ValueTask.FromResult(++attempts < 3 ? ActorAdmissionResult.Rejected(reason) : ActorAdmissionResult.AcceptedResult));
        var result = await NatsProtectedRealtimeDelivery.AdmitAsync(supervisor, message, Subject, default);
        Assert.True(result.Accepted);
        Assert.Equal(3, attempts);
        message.DidNotReceive().Dispose();
    }

    [Fact]
    public async Task Replacement_queue_is_resolved_after_stopping_generation_and_shutdown_cancels_a_full_queue()
    {
        var (supervisor, queues, children) = Fixture();
        var message = Substitute.For<IActorMessage>();
        var replacement = Substitute.For<IActor>();
        var mailbox = Substitute.For<IActorMailbox>();
        var replacementQueues = Substitute.For<IActorThreadQueues>();
        replacement.Mailbox.Returns(mailbox); mailbox.ThreadQueues.Returns(replacementQueues);
        replacementQueues.TryAdmitAsync(message, Subject, Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(ActorAdmissionResult.AcceptedResult));
        queues.TryAdmitAsync(message, Subject, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            children[Subject.ActorId] = replacement;
            return ValueTask.FromResult(ActorAdmissionResult.Rejected(ActorAdmissionReason.Stopping));
        });
        Assert.True((await NatsProtectedRealtimeDelivery.AdmitAsync(supervisor, message, Subject, default)).Accepted);
        replacementQueues.TryAdmitAsync(message, Subject, Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(ActorAdmissionResult.Rejected(ActorAdmissionReason.MailboxLimit)));
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NatsProtectedRealtimeDelivery.AdmitAsync(supervisor, message, Subject, stop.Token).AsTask());
        message.DidNotReceive().Dispose();
    }

    [Fact]
    public async Task Burst_waits_for_capacity_and_preserves_every_input_in_order_without_duplicates()
    {
        var (supervisor, queues, _) = Fixture();
        var open = false;
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new List<IActorMessage>();
        queues.TryAdmitAsync(Arg.Any<IActorMessage>(), Subject, Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (!Volatile.Read(ref open)) { blocked.TrySetResult(); return ValueTask.FromResult(ActorAdmissionResult.Rejected(ActorAdmissionReason.ActorTypeMessageLimit)); }
            received.Add(call.Arg<IActorMessage>());
            return ValueTask.FromResult(ActorAdmissionResult.AcceptedResult);
        });
        var messages = Enumerable.Range(0, 1000).Select(_ => Substitute.For<IActorMessage>()).ToArray();
        var delivering = Deliver();
        await blocked.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Empty(received);
        Volatile.Write(ref open, true);
        await delivering.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(messages, received);
        async Task Deliver()
        {
            foreach (var message in messages)
                Assert.True((await NatsProtectedRealtimeDelivery.AdmitAsync(supervisor, message, Subject, default)).Accepted);
        }
    }

    static (IActorSupervisor Supervisor, IActorThreadQueues Queues, Dictionary<ActorMailboxId, IActor> Children) Fixture()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var actor = Substitute.For<IActor>();
        var mailbox = Substitute.For<IActorMailbox>();
        var queues = Substitute.For<IActorThreadQueues>();
        actor.Mailbox.Returns(mailbox); mailbox.ThreadQueues.Returns(queues);
        var children = new Dictionary<ActorMailboxId, IActor> { [Subject.ActorId] = actor };
        supervisor.Children.Returns(children);
        return (supervisor, queues, children);
    }
}
