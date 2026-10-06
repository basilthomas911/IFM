using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class ActorSupervisorRestartTests
{
    [Fact]
    public async Task Restart_drains_all_accepted_work_then_fences_generation_and_reopens_admission()
    {
        await using var fixture = new Fixture(TimeSpan.FromMilliseconds(2));
        var messages = Enumerable.Range(0, 32).Select(value => new Message(value)).ToArray();
        foreach (var message in messages)
            (await fixture.Actor.Mailbox.ThreadQueues.WriteAsync(message)).Should().BeTrue();

        var restarted = await fixture.Supervisor.RestartAsync(
            fixture.ThreadId, expectedGeneration: 1, TimeSpan.FromSeconds(5));

        restarted.Should().BeTrue();
        fixture.Actor.Processed.Should().BeEquivalentTo(Enumerable.Range(0, 32));
        messages.Should().OnlyContain(message => message.DisposeCount == 1 && message.DeliverySucceeded);
        fixture.Actor.Mailbox.ThreadQueues.GetGeneration(fixture.ThreadId).Should().Be(2);
        fixture.Actor.Mailbox.ThreadQueues.IsAdmissionOpen(fixture.ThreadId).Should().BeTrue();
    }

    [Fact]
    public async Task Stale_generation_is_rejected_without_closing_admission()
    {
        await using var fixture = new Fixture(TimeSpan.Zero);
        fixture.Actor.Mailbox.ThreadQueues.GetThreadQueue(fixture.ThreadId);

        var restarted = await fixture.Supervisor.RestartAsync(
            fixture.ThreadId, expectedGeneration: 2, TimeSpan.FromSeconds(1));

        restarted.Should().BeFalse();
        fixture.Actor.Mailbox.ThreadQueues.GetGeneration(fixture.ThreadId).Should().Be(1);
        fixture.Actor.Mailbox.ThreadQueues.IsAdmissionOpen(fixture.ThreadId).Should().BeTrue();
    }

    [Fact]
    public async Task Drain_timeout_preserves_accepted_work_and_quarantines_admission()
    {
        await using var fixture = new Fixture(TimeSpan.FromMilliseconds(250));
        var message = new Message(1);
        (await fixture.Actor.Mailbox.ThreadQueues.WriteAsync(message)).Should().BeTrue();

        var restarted = await fixture.Supervisor.RestartAsync(
            fixture.ThreadId, expectedGeneration: 1, TimeSpan.FromMilliseconds(25));

        restarted.Should().BeFalse();
        fixture.Actor.Mailbox.ThreadQueues.IsAdmissionOpen(fixture.ThreadId).Should().BeFalse();
        fixture.Actor.Mailbox.ThreadQueues.GetGeneration(fixture.ThreadId).Should().Be(1);
        await fixture.Actor.Completed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        message.DisposeCount.Should().Be(1);
        message.DeliverySucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Retire_drains_work_fences_generation_and_keeps_admission_closed()
    {
        await using var fixture = new Fixture(TimeSpan.FromMilliseconds(2));
        var messages = Enumerable.Range(0, 8).Select(value => new Message(value)).ToArray();
        foreach (var message in messages)
            (await fixture.Actor.Mailbox.ThreadQueues.WriteAsync(message)).Should().BeTrue();

        var retired = await fixture.Supervisor.RetireAsync(
            fixture.ThreadId, expectedGeneration: 1, TimeSpan.FromSeconds(5));

        retired.Should().BeTrue();
        messages.Should().OnlyContain(message => message.DisposeCount == 1 && message.DeliverySucceeded);
        fixture.Actor.Mailbox.ThreadQueues.GetGeneration(fixture.ThreadId).Should().Be(2);
        fixture.Actor.Mailbox.ThreadQueues.IsAdmissionOpen(fixture.ThreadId).Should().BeFalse();
    }

    [Fact]
    public async Task Cancellation_keeps_accepted_work_and_requires_explicit_resume()
    {
        await using var fixture = new Fixture(TimeSpan.FromMilliseconds(250));
        (await fixture.Actor.Mailbox.ThreadQueues.WriteAsync(new Message(1))).Should().BeTrue();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await fixture.Supervisor.RestartAsync(fixture.ThreadId, 1, TimeSpan.FromSeconds(2), cancellation.Token));
        fixture.Actor.Mailbox.ThreadQueues.IsAdmissionOpen(fixture.ThreadId).Should().BeFalse();
        await fixture.Actor.Completed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await fixture.Supervisor.ResumeAsync(fixture.ThreadId);
        fixture.Actor.Mailbox.ThreadQueues.IsAdmissionOpen(fixture.ThreadId).Should().BeTrue();
    }

    [Fact]
    public async Task Stopping_rejections_are_excluded_from_capacity_rejections()
    {
        await using var fixture = new Fixture(TimeSpan.Zero);
        fixture.Actor.Mailbox.ThreadQueues.GetThreadQueue(fixture.ThreadId);
        fixture.Actor.Mailbox.ThreadQueues.PauseAdmission(fixture.ThreadId);
        var result = fixture.Actor.Mailbox.ThreadQueues.TryAdmit(new Message(1), new Message(1).Subject);
        result.Reason.Should().Be(ActorAdmissionReason.Stopping);
        fixture.Actor.Mailbox.Metrics.TryGetMailboxSnapshot(fixture.ThreadId, out var snapshot).Should().BeTrue();
        snapshot!.Rejected.Should().Be(1);
        snapshot.CapacityRejected.Should().Be(0);
    }

    sealed class Fixture : IAsyncDisposable
    {
        public ActorSupervisor Supervisor { get; }
        public RecordingActor Actor { get; }
        public ActorThreadId ThreadId { get; }

        public Fixture(TimeSpan delay)
        {
            var container = new Mock<IContainerInstance>();
            container.Setup(value => value.Resolve<IActorThreadQueue>()).Returns(() => new ActorThreadQueueV2(64));
            Supervisor = new ActorSupervisor(container.Object, NullLogger<ActorSupervisor>.Instance);
            var id = new ActorMailboxId(ActorType.Command, "RestartSafety");
            Actor = new RecordingActor(id, new ActorMailbox(Supervisor, id), delay);
            ThreadId = new(ActorType.Command, "RestartSafety", "entity-1");
            Supervisor.AddActor(Actor);
        }

        public ValueTask DisposeAsync() => Supervisor.DisposeAsync();
    }

    sealed class RecordingActor(ActorMailboxId id, IActorMailbox mailbox, TimeSpan delay) : IActor
    {
        readonly ConcurrentQueue<int> _processed = [];
        public ActorMailboxId Id { get; } = id;
        public IActorMailbox Mailbox { get; } = mailbox;
        public bool IsRunning => true;
        public IReadOnlyCollection<int> Processed => _processed;
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask StartAsync(IActorSupervisor supervisor) => ValueTask.CompletedTask;
        public ValueTask StopAsync() => ValueTask.CompletedTask;
        public ValueTask HandleMessageAsync(IActorMessage message) => HandleMessageAsync(message, message.Subject.ThreadId);
        public async ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId)
        {
            if (delay > TimeSpan.Zero) await Task.Delay(delay).ConfigureAwait(false);
            _processed.Enqueue(((Message)message).Value);
            Completed.TrySetResult();
        }
    }

    sealed class Message(int value) : IActorMessage, IActorDeliveryCompletion
    {
        int _disposed;
        public int Value { get; } = value;
        public int DisposeCount => Volatile.Read(ref _disposed);
        public bool DeliverySucceeded { get; private set; }
        public ActorSubject Subject { get; } = new(ActorType.Command, "RestartSafety", "Run", "entity-1");
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() => Interlocked.Increment(ref _disposed);
        public ValueTask CompleteDeliveryAsync(bool succeeded)
        {
            DeliverySucceeded = succeeded;
            return ValueTask.CompletedTask;
        }
    }
}
