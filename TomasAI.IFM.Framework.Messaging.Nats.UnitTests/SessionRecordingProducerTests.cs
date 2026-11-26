using NATS.Client.JetStream.Models;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class SessionRecordingProducerTests
{
    static IActorSupervisor Registry(ActorSubject subject)
    {
        var supervisor = Substitute.For<IActorSupervisor>(); supervisor.ActorExists(subject.ActorId).Returns(true);
        supervisor.GetRealtimeRoutes(Arg.Any<ActorTypeId>()).Returns(ImmutableArray.Create(
            new RealtimeActorRoute(new(ActorType.Realtime, "SessionTestFanout"), source => "frozen-" + source.EntityId)));
        return supervisor;
    }

    [RealtimeBrokerTheory]
    [InlineData(ActorType.Realtime)]
    [InlineData(ActorType.Event)]
    public async Task Core_publishes_exact_archived_body_once_with_original_headers_and_logical_manifest(ActorType type)
    {
        var subject = new ActorSubject(type, type == ActorType.Realtime ? "SessionWireTest" : "BrokerAccountEvent",
            type == ActorType.Realtime ? "Updated" : "BrokerAccountSnapshotObserved", Guid.NewGuid().ToString("N"));
        var options = new RealtimeSessionArchiveOptions { Enabled = true, SessionId = "producer-test",
            Directory = Path.Combine(Path.GetTempPath(), "ifm-session-producer", Guid.NewGuid().ToString("N")) };
        await using var archive = new DurableRealtimeSessionArchive(options);
        await using var manager = new NatsConnectionManager();
        var client = await manager.GetClientAsync(Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")!);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var subscription = await client.Connection.SubscribeCoreAsync<byte[]>(subject.ToString(),
            serializer: new NatsByteArrayMessageSerializer(), cancellationToken: deadline.Token);
        await client.PingAsync(deadline.Token);
        await using var producer = new NatsActorProducer(new NatsProducerOptions { Url = Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")! },
            NullLogger.Instance, manager, sessionRecorder: archive, sessionSupervisor: () => Registry(subject));
        await producer.StartAsync(subject.ActorId, deadline.Token);
        var value = new MutableEvent(40) { Id = Guid.NewGuid(), Subject = subject, ReceivedOn = new(2026,11,30,1,2,3,DateTimeKind.Utc) };
        var origin = new MessageOrigin(new DateTimeOffset(value.ReceivedOn), "original-session-source"); MessageOrigins.Restore(value, origin);
        using var activity = new Activity("session-producer-test").SetIdFormat(ActivityIdFormat.W3C).Start();
        activity.TraceStateString = "session=original";
        if (type == ActorType.Realtime) await producer.SendAsync(subject, value).AsTask().WaitAsync(deadline.Token);
        else
        {
            var js = await manager.GetJetStreamContextAsync(Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")!, deadline.Token);
            var streamName = "session_account_" + Guid.NewGuid().ToString("N");
            await js.CreateOrUpdateStreamAsync(new StreamConfig(streamName, [subject.ToString()]), deadline.Token);
            try
            {
                await using var durableProducer = new NatsJetStreamActorProducer(new NatsJetStreamProducerOptions { Url = Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")! },
                    NullLogger<NatsJetStreamActorProducer>.Instance, manager, sessionRecorder: archive, sessionSupervisor: () => Registry(subject));
                await durableProducer.StartAsync(subject.ActorId, deadline.Token);
                await durableProducer.SendAsync<MutableEvent, ActorEntityId>(subject, value, deadline.Token);
            }
            finally { await js.DeleteStreamAsync(streamName, deadline.Token); }
        }
        await using var reader = subscription.Msgs.ReadAllAsync(deadline.Token).GetAsyncEnumerator(deadline.Token);
        Assert.True(await reader.MoveNextAsync());
        var saved = archive.ReadCommitted().Single().Record;
        Assert.Equal(saved.OriginalWire.ToArray(), reader.Current.Data); Assert.Equal(1, value.Reads);
        Assert.Equal(origin, saved.Origin); Assert.Equal(origin, global::TomasAI.IFM.Framework.Messaging.Nats.MessageOriginHeaders.Read(reader.Current.Headers));
        Assert.Equal(activity.Id, saved.TraceParent); Assert.Equal(activity.TraceStateString, saved.TraceState);
        Assert.Equal(type == ActorType.Realtime ? 2 : 1, saved.Destinations.Length);
        Assert.Equal(type == ActorType.Realtime ? "RealtimePublication" : "AccountObservation", saved.Kind);
    }

    [Fact]
    public async Task Archive_failure_or_missing_registry_prevents_Core_attempt_before_any_source_is_unowned()
    {
        var subject = new ActorSubject(ActorType.Realtime, "SessionWireTest", "Updated", "only-recording");
        var source = new MutableEvent(1) { Subject = subject, Id = Guid.NewGuid() };
        var recorder = Substitute.For<IRealtimeSessionRecorder>(); recorder.Enabled.Returns(true);
        recorder.RecordAsync(Arg.Any<RealtimeSessionArchiveRecord>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<RealtimeSessionArchiveReceipt>(new IOException("Archive refused source ownership.")));
        await using var producer = new NatsActorProducer(new NatsProducerOptions(), NullLogger.Instance,
            sessionRecorder: recorder, sessionSupervisor: () => Registry(subject));
        // No broker connection exists. A Core attempt would throw NullReferenceException instead.
        await Assert.ThrowsAsync<IOException>(async () => await InvokePublish(producer, subject, source));
        await using var missing = new NatsActorProducer(new NatsProducerOptions(), NullLogger.Instance, sessionRecorder: recorder);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await InvokePublish(missing, subject, source));
    }

    [RealtimeBrokerTheory]
    [InlineData(true)]
    public async Task Compatibility_base_preserves_recorder_and_propagates_recording_failure(bool recording)
    {
        Assert.True(recording);
        var subject = new ActorSubject(ActorType.Realtime, "SessionWireTest", "Updated", Guid.NewGuid().ToString("N"));
        var recorder = Substitute.For<IRealtimeSessionRecorder>(); recorder.Enabled.Returns(true);
        recorder.RecordAsync(Arg.Any<RealtimeSessionArchiveRecord>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<RealtimeSessionArchiveReceipt>(new IOException("Archive refused compatibility source.")));
        await using var producer = new CompatibilityProducer(new NatsProducerOptions { Url = Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")! }, recorder, () => Registry(subject));
        var source = new MutableEvent(7) { Subject = subject, Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), ReceivedOn = DateTime.UtcNow };
        await Assert.ThrowsAsync<IOException>(() => producer.PostEventAsync(source));
        await recorder.Received(1).RecordAsync(Arg.Is<RealtimeSessionArchiveRecord>(record => record.EventId == source.Id && record.Subject == subject.ToString()), Arg.Any<CancellationToken>());
        Assert.Equal(1, source.Reads);
    }

    [Fact]
    public async Task Compatibility_preparation_failure_propagates_when_recording_without_contacting_broker()
    {
        var subject = new ActorSubject(ActorType.Realtime, "SessionWireTest", "Updated", "invalid-command-source");
        var recorder = Substitute.For<IRealtimeSessionRecorder>(); recorder.Enabled.Returns(true);
        await using var producer = new CompatibilityProducer(new NatsProducerOptions { Url = "nats://127.0.0.1:1" }, recorder, () => Registry(subject));
        var source = new MutableEvent(7) { Subject = subject, Id = Guid.NewGuid(), CommandId = Guid.Empty };
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => producer.PostEventAsync(source));
        Assert.Contains("CommandId is empty", failure.Message);
        await recorder.DidNotReceive().RecordAsync(Arg.Any<RealtimeSessionArchiveRecord>(), Arg.Any<CancellationToken>());
    }

    sealed class CompatibilityProducer(NatsProducerOptions options, IRealtimeSessionRecorder recorder, Func<IActorSupervisor> registry)
        : NatsEventProducer(options, NullLogger.Instance, sessionRecorder: recorder, sessionSupervisor: registry)
    {
        public override Task PostEventAsync(IEvent value) => SendEventAsync(value.Subject.EntityId, (MutableEvent)value);
    }

    static ValueTask InvokePublish(NatsActorProducer producer, ActorSubject subject, MutableEvent value)
        => (ValueTask)typeof(NatsActorProducer).GetMethod("PublishAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(typeof(MutableEvent)).Invoke(producer, [subject, value, CancellationToken.None])!;

    [MessagePackObject]
    public sealed record MutableEvent : IEvent<ActorEntityId>
    {
        int original;
        [SerializationConstructor] public MutableEvent() { }
        public MutableEvent(int value) { original = value; }
        [Key(0)] public ActorSubject Subject { get; init; }
        [Key(1)] public Guid Id { get; init; }
        [Key(2)] public long EventId { get; init; }
        [Key(3)] public Guid CommandId { get; init; }
        [Key(4)] public string AggregateId { get; init; } = "";
        [Key(5)] public string EventSource { get; init; } = "session-producer-test";
        [Key(6)] public DateTime ReceivedOn { get; init; }
        [Key(8)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
        [Key(7)] public int Value { get { Reads++; return original + Reads; } init { original = value; } }
        [IgnoreMember] public int Reads { get; private set; }
        [IgnoreMember] public string UserName => "";
        [IgnoreMember] public string EventName => nameof(MutableEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;
    }
}
