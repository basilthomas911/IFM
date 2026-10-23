using Moq;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class LatestOptionSnapshotQueueTests
{
    [Fact]
    public void Full_position_notifications_keep_latest_per_verb_and_dispose_each_eviction_once()
    {
        var queue = new LatestOptionSnapshotQueue(ActorAdmissionController.Disabled);
        queue.SetId(new(ActorType.Realtime, "IronCondorTradePositionRealtime", "trade-a")); queue.Start();
        var messages = Enumerable.Range(0, 10_000).Select(_ => new Message()).ToArray();
        foreach (var message in messages) Assert.True(queue.Write(message));
        var inputs = new Message("IronCondorMonitoringInputsChanged"); Assert.True(queue.Write(inputs));
        Assert.Equal(2, queue.Count); Assert.Equal(9999, queue.Replaced);
        Assert.All(messages[..^1], message => Assert.Equal(1, message.Disposals));
        var remaining = queue.ReadAll().ToArray();
        Assert.Same(messages[^1], remaining[0]); Assert.Same(inputs, remaining[1]);
        Assert.Equal(0, messages[^1].Disposals); // Active message belongs to its handler, not the pending queue.
        foreach (var message in remaining) message.Dispose();
        queue.Stop(); Assert.All(messages, message => Assert.Equal(1, message.Disposals));
    }

    [Fact]
    public void Snapshot_profile_accepts_latest_even_when_shared_capacity_is_exhausted()
    {
        var controller = new ActorAdmissionController(new() { Mode = ActorAdmissionMode.Enforce,
            GlobalMessageLimit = 1, GlobalByteLimit = 100, MaximumPayloadBytes = 100,
            DefaultActorTypeMessageLimit = 1, DefaultActorTypeByteLimit = 100, DefaultMailboxMessageLimit = 1 });
        Assert.True(controller.TryReserve(new Message(), ActorType.Command, out var occupied).Accepted);
        var supervisor = new Mock<IActorSupervisor>(); var thread = new Mock<IActorThread>();
        supervisor.Setup(value => value.GetThread(It.IsAny<ActorThreadId>())).Returns(thread.Object);
        var queues = new ActorThreadQueues(supervisor.Object, admissionController: controller);
        var messages = Enumerable.Range(0, 100).Select(_ => new Message()).ToArray();
        foreach (var message in messages) Assert.True(queues.TryAdmit(message, message.Subject).Accepted);
        Assert.Equal(1, queues.GetThreadQueue(messages[0].Subject.ThreadId).Count);
        Assert.Equal(1, controller.CurrentMessageCount); // Snapshot replacement neither consumes nor leaks the durable budget.
        queues.DiscardRealtimeWork(); controller.Release(occupied);
        Assert.Equal(0, controller.CurrentMessageCount);
        Assert.All(messages, message => Assert.Equal(1, message.Disposals));
    }

    [Fact]
    public void Reply_requests_are_not_coalesced_and_trade_mailboxes_remain_separate()
    {
        var queue = new LatestOptionSnapshotQueue(ActorAdmissionController.Disabled);
        queue.SetId(new(ActorType.Realtime, "IronCondorTradePositionRealtime", "trade-a")); queue.Start();
        var first = new Message(reply: true); var second = new Message(reply: true);
        Assert.True(queue.Write(first)); Assert.True(queue.Write(second)); Assert.Equal(2, queue.Count);
        Assert.False(LatestOptionSnapshotQueue.Applies(new(ActorType.Command, "IronCondorTradePositionRealtime", "trade-a")));
        Assert.False(LatestOptionSnapshotQueue.Applies(new(ActorType.Realtime, "FuturesItiSignalRealtime", "ES")));
        queue.Stop(); Assert.Equal(1, first.Disposals); Assert.Equal(1, second.Disposals);
    }

    [Fact]
    public void Concurrent_producers_remain_bounded_and_release_all_messages_once()
    {
        var queue = new LatestOptionSnapshotQueue(ActorAdmissionController.Disabled);
        queue.SetId(new(ActorType.Realtime, "IronCondorTradePositionRealtime", "trade-a")); queue.Start();
        var messages = Enumerable.Range(0, 5000).Select(_ => new Message()).ToArray();
        Parallel.ForEach(messages, message => Assert.True(queue.Write(message)));
        Assert.Equal(1, queue.Count); queue.Stop();
        Assert.All(messages, message => Assert.Equal(1, message.Disposals));
    }

    sealed class Message(string verb = "StrategyPositionChanged", bool reply = false) : IActorMessage
    {
        public int Disposals;
        public ActorSubject Subject { get; } = new(ActorType.Realtime, "IronCondorTradePositionRealtime", verb, "trade-a");
        public ActorSubject ReplySubject { get; set; }
        public bool CanReply => reply;
        public int AdmissionSizeBytes => 1;
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery,TResult>() where TQuery : class, IQuery<TResult> where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() => Interlocked.Increment(ref Disposals);
    }
}
