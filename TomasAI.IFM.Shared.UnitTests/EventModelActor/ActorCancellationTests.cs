using System;
using System.Collections.Generic;
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

public sealed class ActorCancellationTests
{
    [Fact]
    public async Task ActorService_SendAsync_PropagatesCancellationWithoutConvertingItToFailure()
    {
        var entityId = new ActorEntityId("entity-1");
        var command = new TestCommand(entityId);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var producer = new Mock<IActorProducer>();
        producer
            .Setup(instance => instance.SendAsync<TestCommand, ActorEntityId>(
                command.Subject,
                command,
                entityId,
                cancellation.Token))
            .Returns(ValueTask.FromCanceled(cancellation.Token));

        var supervisor = new Mock<IActorSupervisor>();
        supervisor
            .Setup(instance => instance.GetProducer(command.Subject.ActorId))
            .Returns(producer.Object);

        var service = new ActorService(supervisor.Object);
        Func<Task> operation = () => service
            .SendAsync(command, entityId, cancellation.Token)
            .AsTask();

        await operation.Should().ThrowAsync<OperationCanceledException>();
        producer.Verify(instance => instance.SendAsync<TestCommand, ActorEntityId>(
            command.Subject,
            command,
            entityId,
            cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task SupervisorShutdown_StopsIntakeBeforeActors()
    {
        var order = new List<string>();
        var container = new Mock<IContainerInstance>();
        await using var supervisor = new ActorSupervisor(container.Object, NullLogger<ActorSupervisor>.Instance);
        var consumer = new RecordingConsumer(order);
        var actor = new RecordingActor(order);
        supervisor.AddConsumer(ActorType.Command, consumer);
        supervisor.AddActor(actor);

        await supervisor.ShutdownAsync();

        order.Should().Equal("consumer", "actor");
    }

    [Fact]
    public async Task SupervisorShutdown_WhenCallerCancelsWait_ShutdownContinues()
    {
        var order = new List<string>();
        var container = new Mock<IContainerInstance>();
        await using var supervisor = new ActorSupervisor(container.Object, NullLogger<ActorSupervisor>.Instance);
        var consumer = new RecordingConsumer(order, pauseStop: true);
        var actor = new RecordingActor(order);
        supervisor.AddConsumer(ActorType.Command, consumer);
        supervisor.AddActor(actor);
        using var cancellation = new CancellationTokenSource();

        var shutdown = supervisor.ShutdownAsync(cancellation.Token).AsTask();
        await consumer.StopStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        Func<Task> canceledShutdown = () => shutdown;
        await canceledShutdown.Should().ThrowAsync<OperationCanceledException>();
        order.Should().Equal("consumer");

        consumer.ReleaseStop();
        await supervisor.ShutdownAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        order.Should().Equal("consumer", "actor");
    }

    [Fact]
    public async Task QueryActor_StartupCancellation_ReachesHookAndRollsBackProducer()
    {
        var supervisor = new Mock<IActorSupervisor>();
        var producer = new Mock<IActorProducer>();
        var context = new Mock<IQueryActorContext>();
        var actor = new CancellableQueryActor();
        using var cancellation = new CancellationTokenSource();
        supervisor.Setup(instance => instance.CreateMailbox(actor.Id)).Returns(Mock.Of<IActorMailbox>());
        supervisor.Setup(instance => instance.GetProducer(actor.Id)).Returns(producer.Object);
        supervisor.Setup(instance => instance.CreateQueryActorContext(actor.Id)).Returns(context.Object);

        var startup = actor.StartAsync(supervisor.Object, cancellation.Token).AsTask();
        await actor.StartupEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        Func<Task> waitForStartup = () => startup;
        await waitForStartup.Should().ThrowAsync<OperationCanceledException>();
        actor.IsRunning.Should().BeFalse();
        producer.Verify(instance => instance.StopAsync(), Times.Once);
    }

    [Fact]
    public async Task EventActor_StartupCancellation_ReachesHookAndRollsBackProducer()
    {
        var supervisor = new Mock<IActorSupervisor>();
        var producer = new Mock<IJSActorProducer>();
        var actorId = new ActorMailboxId(ActorType.Event, "StartupCancellation");
        supervisor.Setup(instance => instance.CreateMailbox(actorId)).Returns(Mock.Of<IActorMailbox>());
        supervisor.Setup(instance => instance.GetJSProducer(actorId)).Returns(producer.Object);
        var actor = new CancellableEventActor(supervisor.Object, actorId);
        using var cancellation = new CancellationTokenSource();

        var startup = actor.StartAsync(supervisor.Object, cancellation.Token).AsTask();
        await actor.StartupEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        Func<Task> waitForStartup = () => startup;
        await waitForStartup.Should().ThrowAsync<OperationCanceledException>();
        actor.IsRunning.Should().BeFalse();
        producer.Verify(instance => instance.StopAsync(), Times.Once);
    }

    [Fact]
    public async Task DenormalizerActor_StartupCancellation_ReachesHookAndRollsBackProducer()
    {
        var supervisor = new Mock<IActorSupervisor>();
        var producer = new Mock<IActorProducer>();
        var context = new Mock<IDenormalizerActorContext>();
        var actor = new CancellableDenormalizerActor();
        using var cancellation = new CancellationTokenSource();
        supervisor.Setup(instance => instance.CreateMailbox(actor.Id)).Returns(Mock.Of<IActorMailbox>());
        supervisor.Setup(instance => instance.GetProducer(actor.Id)).Returns(producer.Object);
        supervisor.Setup(instance => instance.CreateDenormalizerActorContext(actor.Id)).Returns(context.Object);

        var startup = actor.StartAsync(supervisor.Object, cancellation.Token).AsTask();
        await actor.StartupEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        Func<Task> waitForStartup = () => startup;
        await waitForStartup.Should().ThrowAsync<OperationCanceledException>();
        actor.IsRunning.Should().BeFalse();
        producer.Verify(instance => instance.StopAsync(), Times.Once);
    }

    sealed record TestCommand(ActorEntityId EntityId) : ICommand<ActorEntityId>
    {
        public ActorSubject Subject { get; init; } = new(ActorType.Command, "CancellationTest", "Run", EntityId.Format());
        public string CommandName => nameof(TestCommand);
        public BoundedContextName RouteTo => BoundedContextName.Undefined;
        public Guid CommandId { get; init; } = Guid.NewGuid();
        public string StreamId => EntityId.Format();
        public string EventSource => nameof(TestCommand);
        public int ErrorCode => 0;
    }

    sealed class CancellableQueryActor()
        : BaseQueryActor<CancellableQueryActor>(
            NullLogger<CancellableQueryActor>.Instance,
            new ActorMailboxId(ActorType.Query, "StartupCancellation"))
    {
        public TaskCompletionSource StartupEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async ValueTask OnStartup(
            IQueryActorContext context,
            CancellationToken cancellationToken)
        {
            StartupEntered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        protected override IQuery ParseMessage(IQueryActorContext context, IActorMessage message)
            => throw new NotSupportedException();

        protected override ValueTask ReceiveAsync(IQueryActorContext context, IQuery query)
            => ValueTask.CompletedTask;

        protected override ValueTask OnExceptionAsync(
            IQueryActorContext context,
            ActorThreadId threadId,
            IQuery query,
            string verb,
            Exception ex)
            => ValueTask.CompletedTask;
    }

    sealed class CancellableEventActor(IActorSupervisor supervisor, ActorMailboxId actorId)
        : BaseEventActor<CancellableEventActor>(
            supervisor,
            NullLogger<CancellableEventActor>.Instance,
            actorId)
    {
        public TaskCompletionSource StartupEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async ValueTask OnStartup(
            IEventActorContext context,
            CancellationToken cancellationToken)
        {
            StartupEntered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        protected override IEvent ParseMessage(IEventActorContext context, IActorMessage message)
            => throw new NotSupportedException();

        protected override ValueTask ReceiveAsync(IEventActorContext context, IEvent @event)
            => ValueTask.CompletedTask;

        protected override ValueTask OnExceptionAsync(
            IEventActorContext context,
            ActorThreadId threadId,
            IEvent @event,
            Exception ex)
            => ValueTask.CompletedTask;
    }

    sealed class CancellableDenormalizerActor()
        : BaseDenormalizerActor<CancellableDenormalizerActor>(
            NullLogger<CancellableDenormalizerActor>.Instance,
            new ActorMailboxId(ActorType.Event, "StartupCancellationDenormalizer"))
    {
        public TaskCompletionSource StartupEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async ValueTask OnStartup(
            IDenormalizerActorContext context,
            CancellationToken cancellationToken)
        {
            StartupEntered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        protected override IEvent ParseMessage(
            IDenormalizerActorContext context,
            NatsMsg<byte[]> message)
            => throw new NotSupportedException();

        protected override ValueTask ReceiveAsync(
            IDenormalizerActorContext context,
            ActorThreadId threadId,
            IEvent @event)
            => ValueTask.CompletedTask;

        protected override ValueTask OnExceptionAsync(
            IDenormalizerActorContext context,
            ActorThreadId threadId,
            IEvent @event,
            Exception ex)
            => ValueTask.CompletedTask;
    }

    sealed class RecordingConsumer(List<string> order, bool pauseStop = false) : IActorConsumer
    {
        readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource StopStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsRunning => false;

        public ValueTask StartAsync(IActorSupervisor context, ActorType actorType, string consumerName = default!)
            => ValueTask.CompletedTask;

        public ValueTask StopAsync() => StopAsync(CancellationToken.None);

        public async ValueTask StopAsync(CancellationToken cancellationToken)
        {
            order.Add("consumer");
            StopStarted.TrySetResult();
            if (pauseStop)
                await _release.Task.ConfigureAwait(false);
        }

        public void ReleaseStop() => _release.TrySetResult();
    }

    sealed class RecordingActor(List<string> order) : IActor
    {
        public ActorMailboxId Id { get; } = new(ActorType.Command, "CancellationTest");
        public IActorMailbox Mailbox => null!;
        public bool IsRunning => true;
        public ValueTask HandleMessageAsync(IActorMessage message) => ValueTask.CompletedTask;
        public ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId) => ValueTask.CompletedTask;
        public ValueTask StartAsync(IActorSupervisor supervisor) => ValueTask.CompletedTask;
        public ValueTask StopAsync()
        {
            order.Add("actor");
            return ValueTask.CompletedTask;
        }
    }
}
