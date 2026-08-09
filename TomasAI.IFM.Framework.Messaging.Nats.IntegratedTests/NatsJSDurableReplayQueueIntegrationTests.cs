using System.Collections.Concurrent;
using System.Text;
using FluentAssertions;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Newtonsoft.Json;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests;

[Trait("Category", "Integration")]
public sealed class NatsJSDurableReplayQueueIntegrationTests : IAsyncLifetime
{
    static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    static readonly TimeSpan ReplayInterval = TimeSpan.FromMilliseconds(100);
    readonly ConcurrentBag<QueueResources> _resources = [];
    readonly string _url = Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";
    NatsClient _adminClient = null!;
    INatsJSContext _jetStream = null!;

    public async Task InitializeAsync()
    {
        _adminClient = new NatsClient(_url);
        await _adminClient.ConnectAsync();
        _jetStream = _adminClient.CreateJetStreamContext();
    }

    [Fact]
    public async Task StartAsync_configures_process_consumer_for_unlimited_redelivery()
    {
        var resources = CreateResources();
        await using var queue = CreateQueue();

        await queue.StartAsync(resources.ProjectorName, ReplayInterval);

        var consumer = await _jetStream.GetConsumerAsync(
            resources.ProcessStream,
            resources.ProcessConsumer);
        consumer.Info.Config.MaxDeliver.Should().Be(-1);
        consumer.Info.Config.AckPolicy.Should().Be(ConsumerConfigAckPolicy.Explicit);
    }

    [Fact]
    public async Task Enqueue_same_event_twice_is_stored_and_processed_once()
    {
        var resources = CreateResources();
        await using var queue = CreateQueue();
        var processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        await queue.StartAsync(resources.ProjectorName, ReplayInterval);
        await queue.DequeueAsync(resources.ProjectorName, _ =>
        {
            Interlocked.Increment(ref calls);
            processed.TrySetResult();
            return Task.CompletedTask;
        });
        var domainEvent = CreateEvent("duplicate");

        await queue.EnqueueAsync(resources.ProjectorName, domainEvent);
        await queue.EnqueueAsync(resources.ProjectorName, domainEvent);

        await processed.Task.WaitAsync(TestTimeout);
        await Task.Delay(250);
        calls.Should().Be(1);
        var stream = await _jetStream.GetStreamAsync(resources.ProcessStream);
        stream.Info.State.Messages.Should().Be(1);
    }

    [Fact]
    public async Task Replay_publish_failure_redelivers_process_message_and_completes_handoff()
    {
        var resources = CreateResources();
        var innerTransport = new NatsJSDurableQueueTransport(CreateOptions());
        var transport = new FailFirstReplayPublishTransport(innerTransport);
        await using var queue = new NatsJSDurableReplayQueue(transport, TimeSpan.FromMinutes(1));
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        await queue.StartAsync(resources.ProjectorName, ReplayInterval);
        await queue.DequeueAsync(resources.ProjectorName, _ =>
        {
            if (Interlocked.Increment(ref calls) <= 2)
                throw new InvalidOperationException("projection failed");
            completed.TrySetResult();
            return Task.CompletedTask;
        });

        await queue.EnqueueAsync(resources.ProjectorName, CreateEvent("handoff"));

        await completed.Task.WaitAsync(TestTimeout);
        calls.Should().Be(3);
        transport.ReplayPublishAttempts.Should().Be(2);
        var processConsumer = await _jetStream.GetConsumerAsync(
            resources.ProcessStream,
            resources.ProcessConsumer);
        processConsumer.Info.Delivered.ConsumerSeq.Should().BeGreaterThanOrEqualTo(2);
        var replayStream = await _jetStream.GetStreamAsync(resources.ReplayStream);
        replayStream.Info.State.Messages.Should().Be(1);
    }

    [Fact]
    public async Task Process_message_is_consumed_after_transport_restart()
    {
        var resources = CreateResources();
        var settings = CreateSettings(resources, TimeSpan.FromSeconds(30), 3);
        var domainEvent = CreateEvent("restart");
        var payload = Serialize(domainEvent, resources.ProjectorName);
        var messageId = $"{resources.ProjectorName}:process:event-{domainEvent.EventId}";
        await using (var publisher = new NatsJSDurableQueueTransport(CreateOptions()))
        {
            await publisher.EnsureQueueAsync(resources.ProjectorName, settings, CancellationToken.None);
            await publisher.PublishProcessAsync(
                resources.ProjectorName,
                payload,
                messageId,
                CancellationToken.None);
        }

        await using var queue = CreateQueue();
        var received = new TaskCompletionSource<IntegrationEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await queue.DequeueAsync(resources.ProjectorName, domainEventValue =>
        {
            received.TrySetResult((IntegrationEvent)domainEventValue);
            return Task.CompletedTask;
        });

        var recovered = await received.Task.WaitAsync(TestTimeout);
        recovered.Id.Should().Be(domainEvent.Id);
        recovered.EventId.Should().Be(domainEvent.EventId);
        recovered.Value.Should().Be("restart");
    }

    NatsJSDurableReplayQueue CreateQueue() => new(CreateOptions());

    NatsJetStreamConsumerOptions CreateOptions() => new()
    {
        Url = _url
    };

    QueueResources CreateResources()
    {
        var safeName = $"integration_{Guid.NewGuid():N}";
        var resources = new QueueResources(
            safeName,
            $"IFM_{safeName}_PROCESS",
            $"ifm.projector.{safeName}.process",
            $"{safeName}-process-worker",
            $"IFM_{safeName}_REPLAY",
            $"ifm.projector.{safeName}.replay",
            $"{safeName}-replay-worker");
        _resources.Add(resources);
        return resources;
    }

    static NatsJSDurableQueueSettings CreateSettings(
        QueueResources resources,
        TimeSpan replayInterval,
        int maxReplayAttempts) =>
        new(
            new NatsJSDurableQueueNames(
                resources.ProcessStream,
                resources.ProcessSubject,
                resources.ProcessConsumer,
                resources.ReplayStream,
                resources.ReplaySubject,
                resources.ReplayConsumer),
            replayInterval,
            maxReplayAttempts,
            Enumerable.Range(0, maxReplayAttempts)
                .Select(attempt => TimeSpan.FromTicks(Math.Min(
                    replayInterval.Ticks * (1L << Math.Min(attempt, 6)),
                    TimeSpan.FromMinutes(2).Ticks)))
                .ToArray());

    static IntegrationEvent CreateEvent(string value) => new()
    {
        Subject = ActorSubject.Default,
        Id = Guid.NewGuid(),
        EventId = Random.Shared.NextInt64(1, long.MaxValue),
        CommandId = Guid.NewGuid(),
        AggregateId = $"aggregate-{value}",
        EventSource = "nats-integration-tests",
        ReceivedOn = DateTime.UtcNow,
        Value = value
    };

    static byte[] Serialize(IntegrationEvent domainEvent, string eventProjectorName)
    {
        var envelope = new
        {
            EventProjectorName = eventProjectorName,
            EventType = domainEvent.GetType().AssemblyQualifiedName,
            EventJson = JsonConvert.SerializeObject(domainEvent, domainEvent.GetType(), SerializerSettings),
            EnqueuedAtUtc = DateTimeOffset.UtcNow,
            FailedAtUtc = (DateTimeOffset?)null,
            ErrorMessage = (string?)null
        };
        return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelope, SerializerSettings));
    }

    static readonly JsonSerializerSettings SerializerSettings = new()
    {
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
    };

    public async Task DisposeAsync()
    {
        foreach (var resources in _resources)
        {
            await TryDeleteStreamAsync(resources.ProcessStream);
            await TryDeleteStreamAsync(resources.ReplayStream);
        }
        await _adminClient.DisposeAsync();
    }

    async Task TryDeleteStreamAsync(string streamName)
    {
        try
        {
            await _jetStream.DeleteStreamAsync(streamName);
        }
        catch (NatsJSApiException)
        {
        }
    }

    sealed record QueueResources(
        string ProjectorName,
        string ProcessStream,
        string ProcessSubject,
        string ProcessConsumer,
        string ReplayStream,
        string ReplaySubject,
        string ReplayConsumer);

    sealed class FailFirstReplayPublishTransport(INatsJSDurableQueueTransport inner)
        : INatsJSDurableQueueTransport
    {
        int _failuresRemaining = 1;
        int _replayPublishAttempts;

        public int ReplayPublishAttempts => Volatile.Read(ref _replayPublishAttempts);

        public ValueTask EnsureQueueAsync(
            string eventProjectorName,
            NatsJSDurableQueueSettings settings,
            CancellationToken cancellationToken) =>
            inner.EnsureQueueAsync(eventProjectorName, settings, cancellationToken);

        public ValueTask PublishProcessAsync(
            string eventProjectorName,
            byte[] payload,
            string messageId,
            CancellationToken cancellationToken) =>
            inner.PublishProcessAsync(eventProjectorName, payload, messageId, cancellationToken);

        public ValueTask PublishReplayAsync(
            string eventProjectorName,
            byte[] payload,
            string messageId,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _replayPublishAttempts);
            if (Interlocked.Exchange(ref _failuresRemaining, 0) == 1)
                throw new InvalidOperationException("Injected replay publication failure.");
            return inner.PublishReplayAsync(eventProjectorName, payload, messageId, cancellationToken);
        }

        public IAsyncEnumerable<INatsJSDurableMessage> ConsumeProcessAsync(
            string eventProjectorName,
            CancellationToken cancellationToken) =>
            inner.ConsumeProcessAsync(eventProjectorName, cancellationToken);

        public IAsyncEnumerable<INatsJSDurableMessage> ConsumeReplayAsync(
            string eventProjectorName,
            CancellationToken cancellationToken) =>
            inner.ConsumeReplayAsync(eventProjectorName, cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}

public sealed class IntegrationEvent : IEvent
{
    public ActorSubject Subject { get; init; }
    public Guid Id { get; init; }
    public long EventId { get; init; }
    public Guid CommandId { get; init; }
    public string AggregateId { get; init; } = string.Empty;
    public string EventSource { get; init; } = string.Empty;
    public DateTime ReceivedOn { get; init; }
    public string UserName { get; init; } = "integration-test";
    public string EventName => nameof(IntegrationEvent);
    public EventType EventType => EventType.DomainEvent;
    public string Value { get; init; } = string.Empty;
}
