using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using ActorCommandExceptionEvent = TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent;
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
            .Setup(instance => instance.RequestAsync<TestCommand, ActorEntityId, GuidResult>(
                command.Subject,
                command,
                entityId,
                cancellation.Token))
            .Returns(ValueTask.FromCanceled<ServiceResult<GuidResult>>(cancellation.Token));

        var supervisor = new Mock<IActorSupervisor>();
        supervisor
            .Setup(instance => instance.GetProducer(command.Subject.ActorId))
            .Returns(producer.Object);

        var service = new ActorService(supervisor.Object);
        Func<Task> operation = () => service
            .SendAsync(command, entityId, cancellation.Token)
            .AsTask();

        await operation.Should().ThrowAsync<OperationCanceledException>();
        producer.Verify(instance => instance.RequestAsync<TestCommand, ActorEntityId, GuidResult>(
            command.Subject,
            command,
            entityId,
            cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task ActorService_SendAsync_UsesRequestReplyAndPreservesOverloadFailure()
    {
        var entityId = new ActorEntityId("entity-overload");
        var command = new TestCommand(entityId);
        var producer = new Mock<IActorProducer>();
        producer
            .Setup(instance => instance.RequestAsync<TestCommand, ActorEntityId, GuidResult>(
                command.Subject,
                command,
                entityId,
                CancellationToken.None))
            .ReturnsAsync(new ServiceResult<GuidResult>(-429, "temporarily unavailable"));
        var supervisor = new Mock<IActorSupervisor>();
        supervisor
            .Setup(instance => instance.GetProducer(command.Subject.ActorId))
            .Returns(producer.Object);

        var result = await new ActorService(supervisor.Object).SendAsync(command, entityId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(-429);
        result.ErrorMessage.Should().Be("temporarily unavailable");
        producer.Verify(instance => instance.SendAsync<TestCommand, ActorEntityId>(
            It.IsAny<ActorSubject>(),
            It.IsAny<TestCommand>(),
            It.IsAny<ActorEntityId>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void CommandExceptionWithoutCommand_IsRoutedAsDurableEvent()
    {
        var errorEvent = new InvalidOperationException("test")
            .GetCommandExceptionEvent<ActorCommandExceptionEvent, ActorEntityId>(
                ErrorType.Command,
                null!,
                ActorEntityId.Default,
                "ErrorActor",
                ActorCommandExceptionEvent.CommandFail);

        errorEvent.Subject.ActorType.Should().Be(ActorType.Event);
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
        supervisor.SetReadiness(true);

        supervisor.IsReady.Should().BeTrue();

        await supervisor.ShutdownAsync();

        supervisor.IsReady.Should().BeFalse();
        order.Should().Equal("consumer", "actor");
    }

    [Fact]
    public async Task SupervisorShutdown_StopsRealtimeIngressBeforeRequestReplyConsumers()
    {
        var order = new List<string>();
        var container = new Mock<IContainerInstance>();
        await using var supervisor = new ActorSupervisor(container.Object, NullLogger<ActorSupervisor>.Instance);
        supervisor.AddConsumer(ActorType.Realtime, new RecordingConsumer(order, label: "realtime"));
        supervisor.AddConsumer(ActorType.Command, new RecordingConsumer(order, label: "command"));
        supervisor.AddActor(new RecordingActor(order));

        await supervisor.ShutdownAsync();

        order.Should().Equal("realtime", "command", "actor");
    }

    [Fact]
    public async Task SupervisorShutdown_EmitsLifecycleMetrics()
    {
        using var metrics = new LifecycleMetricCollector();
        var container = new Mock<IContainerInstance>();
        await using var supervisor = new ActorSupervisor(container.Object, NullLogger<ActorSupervisor>.Instance);

        await supervisor.ShutdownAsync();

        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == "ifm.actor.shutdown.completed" && measurement.Value == 1);
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == "ifm.actor.shutdown.duration" && measurement.Value >= 0);
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == "ifm.actor.shutdown.messages_drained" && measurement.Value >= 0);
    }

    [Fact]
    public async Task SupervisorShutdown_WhenActorStopFails_EmitsFailureMetrics()
    {
        using var metrics = new LifecycleMetricCollector();
        var container = new Mock<IContainerInstance>();
        var supervisor = new ActorSupervisor(container.Object, NullLogger<ActorSupervisor>.Instance);
        supervisor.AddActor(new RecordingActor([], failStop: true));

        Func<Task> shutdown = () => supervisor.ShutdownAsync().AsTask();

        await shutdown.Should().ThrowAsync<AggregateException>();
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == "ifm.actor.shutdown.failures" && measurement.Value == 1);
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == "ifm.actor.shutdown.cleanup_failures" &&
            measurement.Stage == "actors");
    }

    [Fact]
    public async Task SupervisorShutdown_WhenCallerCancelsWait_ShutdownContinues()
    {
        using var metrics = new LifecycleMetricCollector();
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
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == "ifm.actor.lifecycle.cancellations" &&
            measurement.Phase == "shutdown_wait");

        consumer.ReleaseStop();
        await supervisor.ShutdownAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        order.Should().Equal("consumer", "actor");
    }

    [Fact]
    public async Task QueryActor_StartupCancellation_ReachesHookAndRollsBackProducer()
    {
        var supervisor = new Mock<IActorSupervisor>();
        var producer = new Mock<IActorProducer>();
        var actorId = new ActorMailboxId(ActorType.Query, "StartupCancellation");
        var context = new CancellableQueryContext(supervisor.Object, actorId);
        var actor = new CancellableQueryActor(context);
        using var cancellation = new CancellationTokenSource();
        supervisor.Setup(instance => instance.CreateMailbox(actor.Id)).Returns(Mock.Of<IActorMailbox>());
        supervisor.Setup(instance => instance.GetProducer(actor.Id)).Returns(producer.Object);

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
        var context = new CancellableEventContext(supervisor.Object, actorId);
        var actor = new CancellableEventActor(context);
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
        var actorId = new ActorMailboxId(ActorType.Event, "StartupCancellationDenormalizer");
        var context = new CancellableDenormalizerContext(supervisor.Object, actorId);
        var actor = new CancellableDenormalizerActor(context);
        using var cancellation = new CancellationTokenSource();
        supervisor.Setup(instance => instance.CreateMailbox(actor.Id)).Returns(Mock.Of<IActorMailbox>());
        supervisor.Setup(instance => instance.GetProducer(actor.Id)).Returns(producer.Object);

        var startup = actor.StartAsync(supervisor.Object, cancellation.Token).AsTask();
        await actor.StartupEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        Func<Task> waitForStartup = () => startup;
        await waitForStartup.Should().ThrowAsync<OperationCanceledException>();
        actor.IsRunning.Should().BeFalse();
        producer.Verify(instance => instance.StopAsync(), Times.Once);
    }

    [Fact]
    public async Task RuntimeStartup_CancellationBetweenActorRegistrations_ShutsDownPartialRuntime()
    {
        using var metrics = new LifecycleMetricCollector();
        var container = new Mock<IContainerInstance>();
        var supervisor = new Mock<IActorSupervisor>();
        var registry = new Mock<IActorRegistry>();
        var factory = new Mock<IActorFactory>();
        var producer = new Mock<IActorProducer>();
        var firstActor = new Mock<IActor>();
        var firstActorId = new ActorMailboxId(ActorType.Command, "FirstStartupActor");
        firstActor.SetupGet(instance => instance.Id).Returns(firstActorId);
        registry.SetupGet(instance => instance.ActorTypes).Returns(
            [typeof(FirstRegistrationMarker), typeof(SecondRegistrationMarker)]);
        using var cancellation = new CancellationTokenSource();
        factory.Setup(instance => instance.GetActor(typeof(FirstRegistrationMarker)))
            .Returns(() =>
            {
                cancellation.Cancel();
                return firstActor.Object;
            });
        container.Setup(instance => instance.Resolve<IActorRegistry>()).Returns(registry.Object);
        container.Setup(instance => instance.Resolve<IActorFactory>()).Returns(factory.Object);
        container.Setup(instance => instance.Resolve<IActorProducer>()).Returns(producer.Object);
        supervisor.SetupGet(instance => instance.Container).Returns(container.Object);
        supervisor.Setup(instance => instance.ShutdownAsync(CancellationToken.None))
            .Returns(ValueTask.CompletedTask);

        Func<Task> start = () => ActorRuntimeStartup
            .StartAsync(supervisor.Object, NullLogger.Instance, cancellation.Token)
            .AsTask();

        await start.Should().ThrowAsync<OperationCanceledException>();
        supervisor.Verify(instance => instance.AddActor(firstActor.Object), Times.Once);
        supervisor.Verify(instance => instance.AddProducer(firstActorId, producer.Object), Times.Once);
        factory.Verify(instance => instance.GetActor(typeof(SecondRegistrationMarker)), Times.Never);
        supervisor.Verify(instance => instance.StartConsumersAsync(It.IsAny<CancellationToken>()), Times.Never);
        supervisor.Verify(instance => instance.ShutdownAsync(CancellationToken.None), Times.Once);
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == "ifm.actor.lifecycle.cancellations" &&
            measurement.Phase == "startup");
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == "ifm.actor.startup.duration" && measurement.Value >= 0);
    }

    [Fact]
    public async Task RuntimeStartup_KeepsConsumerIntakeClosedUntilActorStartupCompletes()
    {
        var container = new Mock<IContainerInstance>();
        var supervisor = new Mock<IActorSupervisor>();
        var registry = new Mock<IActorRegistry>();
        var factory = new Mock<IActorFactory>();
        var producer = new Mock<IActorProducer>();
        var consumer = new Mock<IActorConsumer>();
        var jsConsumer = new Mock<IJSActorConsumer>();
        var actor = new Mock<IActor>();
        var actorId = new ActorMailboxId(ActorType.Command, "ReadinessActor");
        var startupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStartup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        actor.SetupGet(instance => instance.Id).Returns(actorId);
        actor.Setup(instance => instance.StartAsync(supervisor.Object, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                startupEntered.TrySetResult();
                return new ValueTask(releaseStartup.Task);
            });
        registry.SetupGet(instance => instance.ActorTypes).Returns([typeof(FirstRegistrationMarker)]);
        factory.Setup(instance => instance.GetActor(typeof(FirstRegistrationMarker))).Returns(actor.Object);
        container.Setup(instance => instance.Resolve<IActorRegistry>()).Returns(registry.Object);
        container.Setup(instance => instance.Resolve<IActorFactory>()).Returns(factory.Object);
        container.Setup(instance => instance.Resolve<IActorProducer>()).Returns(producer.Object);
        container.Setup(instance => instance.Resolve<IActorConsumer>()).Returns(consumer.Object);
        container.Setup(instance => instance.Resolve<IJSActorConsumer>()).Returns(jsConsumer.Object);
        supervisor.SetupGet(instance => instance.Container).Returns(container.Object);
        supervisor.Setup(instance => instance.StartConsumersAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var startup = ActorRuntimeStartup
            .StartAsync(supervisor.Object, NullLogger.Instance)
            .AsTask();
        await startupEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));

        supervisor.Verify(instance => instance.StartConsumersAsync(It.IsAny<CancellationToken>()), Times.Never);
        supervisor.Verify(instance => instance.SetReadiness(true), Times.Never);

        releaseStartup.TrySetResult();
        await startup.WaitAsync(TimeSpan.FromSeconds(1));

        supervisor.Verify(instance => instance.StartConsumersAsync(CancellationToken.None), Times.Once);
        supervisor.Verify(instance => instance.SetReadiness(false), Times.Once);
        supervisor.Verify(instance => instance.SetReadiness(true), Times.Once);
    }

    [Fact]
    public async Task RuntimeStartup_RegistersOneConsumerForEachRegisteredActorType()
    {
        var container = new Mock<IContainerInstance>();
        var supervisor = new Mock<IActorSupervisor>();
        var registry = new Mock<IActorRegistry>();
        var factory = new Mock<IActorFactory>();
        var producer = new Mock<IActorProducer>();
        var jsProducer = new Mock<IJSActorProducer>();
        var consumer = new Mock<IActorConsumer>();
        var jsConsumer = new Mock<IJSActorConsumer>();
        var commandOne = CreateActor(ActorType.Command, "CommandOne");
        var commandTwo = CreateActor(ActorType.Command, "CommandTwo");
        var query = CreateActor(ActorType.Query, "Query");
        var realtime = CreateActor(ActorType.Realtime, "Realtime");
        var @event = CreateActor(ActorType.Event, "Event");
        Type[] registrations =
        [
            typeof(FirstRegistrationMarker),
            typeof(SecondRegistrationMarker),
            typeof(QueryRegistrationMarker),
            typeof(RealtimeRegistrationMarker),
            typeof(EventRegistrationMarker)
        ];

        registry.SetupGet(instance => instance.ActorTypes).Returns(registrations);
        factory.Setup(instance => instance.GetActor(typeof(FirstRegistrationMarker))).Returns(commandOne.Object);
        factory.Setup(instance => instance.GetActor(typeof(SecondRegistrationMarker))).Returns(commandTwo.Object);
        factory.Setup(instance => instance.GetActor(typeof(QueryRegistrationMarker))).Returns(query.Object);
        factory.Setup(instance => instance.GetActor(typeof(RealtimeRegistrationMarker))).Returns(realtime.Object);
        factory.Setup(instance => instance.GetActor(typeof(EventRegistrationMarker))).Returns(@event.Object);
        container.Setup(instance => instance.Resolve<IActorRegistry>()).Returns(registry.Object);
        container.Setup(instance => instance.Resolve<IActorFactory>()).Returns(factory.Object);
        container.Setup(instance => instance.Resolve<IActorProducer>()).Returns(producer.Object);
        container.Setup(instance => instance.Resolve<IJSActorProducer>()).Returns(jsProducer.Object);
        container.Setup(instance => instance.Resolve<IActorConsumer>()).Returns(consumer.Object);
        container.Setup(instance => instance.Resolve<IJSActorConsumer>()).Returns(jsConsumer.Object);
        supervisor.SetupGet(instance => instance.Container).Returns(container.Object);
        supervisor.Setup(instance => instance.StartConsumersAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        await ActorRuntimeStartup.StartAsync(supervisor.Object, NullLogger.Instance);

        supervisor.Verify(instance => instance.AddConsumer(ActorType.Command, consumer.Object), Times.Once);
        supervisor.Verify(instance => instance.AddConsumer(ActorType.Query, consumer.Object), Times.Once);
        supervisor.Verify(instance => instance.AddConsumer(ActorType.Realtime, consumer.Object), Times.Once);
        supervisor.Verify(instance => instance.AddConsumer(ActorType.Notify, It.IsAny<IActorConsumer>()), Times.Never);
        supervisor.Verify(instance => instance.AddConsumer(ActorType.Event, jsConsumer.Object), Times.Once);
    }

    [Fact]
    public async Task RuntimeStartup_RejectsNotifyActors()
    {
        var container = new Mock<IContainerInstance>();
        var supervisor = new Mock<IActorSupervisor>();
        var registry = new Mock<IActorRegistry>();
        var factory = new Mock<IActorFactory>();
        var notify = CreateActor(ActorType.Notify, "StatusConsoleEvent");
        registry.SetupGet(instance => instance.ActorTypes).Returns([typeof(FirstRegistrationMarker)]);
        factory.Setup(instance => instance.GetActor(typeof(FirstRegistrationMarker))).Returns(notify.Object);
        container.Setup(instance => instance.Resolve<IActorRegistry>()).Returns(registry.Object);
        container.Setup(instance => instance.Resolve<IActorFactory>()).Returns(factory.Object);
        container.Setup(instance => instance.Resolve<IActorProducer>()).Returns(Mock.Of<IActorProducer>());
        supervisor.SetupGet(instance => instance.Container).Returns(container.Object);
        supervisor.Setup(instance => instance.ShutdownAsync(CancellationToken.None))
            .Returns(ValueTask.CompletedTask);

        Func<Task> start = () => ActorRuntimeStartup
            .StartAsync(supervisor.Object, NullLogger.Instance)
            .AsTask();

        await start.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Notify subjects are reserved for UI, console, and external NATS event listeners*");
        supervisor.Verify(instance => instance.AddConsumer(
            ActorType.Notify,
            It.IsAny<IActorConsumer>()), Times.Never);
        supervisor.Verify(instance => instance.ShutdownAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task RuntimeStartup_ActorFailureLeavesRuntimeUnreadyAndRollsBackWithoutOpeningIntake()
    {
        var container = new Mock<IContainerInstance>();
        var supervisor = new Mock<IActorSupervisor>();
        var registry = new Mock<IActorRegistry>();
        var factory = new Mock<IActorFactory>();
        var actor = new Mock<IActor>();
        var actorId = new ActorMailboxId(ActorType.Command, "FailedReadinessActor");
        actor.SetupGet(instance => instance.Id).Returns(actorId);
        actor.Setup(instance => instance.StartAsync(supervisor.Object, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("startup failed"));
        registry.SetupGet(instance => instance.ActorTypes).Returns([typeof(FirstRegistrationMarker)]);
        factory.Setup(instance => instance.GetActor(typeof(FirstRegistrationMarker))).Returns(actor.Object);
        container.Setup(instance => instance.Resolve<IActorRegistry>()).Returns(registry.Object);
        container.Setup(instance => instance.Resolve<IActorFactory>()).Returns(factory.Object);
        container.Setup(instance => instance.Resolve<IActorProducer>()).Returns(Mock.Of<IActorProducer>());
        container.Setup(instance => instance.Resolve<IActorConsumer>()).Returns(Mock.Of<IActorConsumer>());
        container.Setup(instance => instance.Resolve<IJSActorConsumer>()).Returns(Mock.Of<IJSActorConsumer>());
        supervisor.SetupGet(instance => instance.Container).Returns(container.Object);
        supervisor.Setup(instance => instance.ShutdownAsync(CancellationToken.None))
            .Returns(ValueTask.CompletedTask);

        Func<Task> start = () => ActorRuntimeStartup
            .StartAsync(supervisor.Object, NullLogger.Instance)
            .AsTask();

        await start.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("startup failed");
        supervisor.Verify(instance => instance.StartConsumersAsync(It.IsAny<CancellationToken>()), Times.Never);
        supervisor.Verify(instance => instance.SetReadiness(true), Times.Never);
        supervisor.Verify(instance => instance.SetReadiness(false), Times.AtLeastOnce);
        supervisor.Verify(instance => instance.ShutdownAsync(CancellationToken.None), Times.Once);
    }

    readonly record struct LifecycleMeasurement(
        string Name,
        double Value,
        string? Phase,
        string? Stage);

    static Mock<IActor> CreateActor(ActorType actorType, string name)
    {
        var actor = new Mock<IActor>();
        actor.SetupGet(instance => instance.Id).Returns(new ActorMailboxId(actorType, name));
        actor.Setup(instance => instance.StartAsync(
                It.IsAny<IActorSupervisor>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        return actor;
    }

    sealed class QueryRegistrationMarker;
    sealed class RealtimeRegistrationMarker;
    sealed class EventRegistrationMarker;

    sealed class LifecycleMetricCollector : IDisposable
    {
        const string ActorMeterName = "TomasAI.IFM.Shared.EventModelActor";
        readonly MeterListener _listener = new();

        public LifecycleMetricCollector()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ActorMeterName)
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                Capture(instrument, value, tags));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                Capture(instrument, value, tags));
            _listener.Start();
        }

        public ConcurrentQueue<LifecycleMeasurement> Measurements { get; } = new();

        void Capture(
            Instrument instrument,
            double value,
            ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            string? phase = null;
            string? stage = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "phase")
                    phase = tag.Value as string;
                else if (tag.Key == "stage")
                    stage = tag.Value as string;
            }

            Measurements.Enqueue(new LifecycleMeasurement(instrument.Name, value, phase, stage));
        }

        public void Dispose() => _listener.Dispose();
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

    sealed class FirstRegistrationMarker;
    sealed class SecondRegistrationMarker;

    sealed class CancellableQueryActor(IQueryActorContext<CancellableQueryActor> actorContext)
        : BaseQueryActor<CancellableQueryActor>(
            actorContext,
            NullLogger<CancellableQueryActor>.Instance)
    {
        public TaskCompletionSource StartupEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async ValueTask OnStartup(
            IQueryActorContext<CancellableQueryActor> context,
            CancellationToken cancellationToken)
        {
            StartupEntered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        protected override IQuery ParseMessage(IQueryActorContext<CancellableQueryActor> context, IActorMessage message)
            => throw new NotSupportedException();

        protected override ValueTask ReceiveAsync(IQueryActorContext<CancellableQueryActor> context, IQuery query)
            => ValueTask.CompletedTask;

        protected override ValueTask OnExceptionAsync(
            IQueryActorContext<CancellableQueryActor> context,
            ActorThreadId threadId,
            IQuery query,
            string verb,
            Exception ex)
            => ValueTask.CompletedTask;
    }

    sealed class CancellableEventActor(IEventActorContext<CancellableEventActor> actorContext)
        : BaseEventActor<CancellableEventActor>(
            actorContext,
            NullLogger<CancellableEventActor>.Instance)
    {
        public TaskCompletionSource StartupEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async ValueTask OnStartup(
            IEventActorContext<CancellableEventActor> context,
            CancellationToken cancellationToken)
        {
            StartupEntered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        protected override IEvent ParseMessage(IEventActorContext<CancellableEventActor> context, IActorMessage message)
            => throw new NotSupportedException();

        protected override ValueTask ReceiveAsync(IEventActorContext<CancellableEventActor> context, IEvent @event)
            => ValueTask.CompletedTask;

        protected override ValueTask OnExceptionAsync(
            IEventActorContext<CancellableEventActor> context,
            ActorThreadId threadId,
            IEvent @event,
            Exception ex)
            => ValueTask.CompletedTask;
    }

    sealed class CancellableDenormalizerActor(
        IDenormalizerActorContext<CancellableDenormalizerActor> actorContext)
        : BaseDenormalizerActor<CancellableDenormalizerActor>(
            actorContext,
            NullLogger<CancellableDenormalizerActor>.Instance)
    {
        public TaskCompletionSource StartupEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async ValueTask OnStartup(
            IDenormalizerActorContext<CancellableDenormalizerActor> context,
            CancellationToken cancellationToken)
        {
            StartupEntered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        protected override IEvent ParseMessage(
            IDenormalizerActorContext<CancellableDenormalizerActor> context,
            NatsMsg<byte[]> message)
            => throw new NotSupportedException();

        protected override ValueTask ReceiveAsync(
            IDenormalizerActorContext<CancellableDenormalizerActor> context,
            ActorThreadId threadId,
            IEvent @event)
            => ValueTask.CompletedTask;

        protected override ValueTask OnExceptionAsync(
            IDenormalizerActorContext<CancellableDenormalizerActor> context,
            ActorThreadId threadId,
            IEvent @event,
            Exception ex)
            => ValueTask.CompletedTask;
    }

    sealed class CancellableQueryContext(IActorSupervisor supervisor, ActorMailboxId actorId)
        : QueryActorContext(supervisor, actorId), IQueryActorContext<CancellableQueryActor>
    {
    }

    sealed class CancellableEventContext(IActorSupervisor supervisor, ActorMailboxId actorId)
        : EventActorContext(supervisor, actorId), IEventActorContext<CancellableEventActor>
    {
    }

    sealed class CancellableDenormalizerContext(IActorSupervisor supervisor, ActorMailboxId actorId)
        : DenormalizerActorContext(supervisor, actorId),
          IDenormalizerActorContext<CancellableDenormalizerActor>
    {
    }

    sealed class RecordingConsumer(
        List<string> order,
        bool pauseStop = false,
        string label = "consumer") : IActorConsumer
    {
        readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource StopStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsRunning => false;

        public ValueTask StartAsync(IActorSupervisor context, ActorType actorType, string consumerName = default!)
            => ValueTask.CompletedTask;

        public ValueTask StopAsync() => StopAsync(CancellationToken.None);

        public async ValueTask StopAsync(CancellationToken cancellationToken)
        {
            order.Add(label);
            StopStarted.TrySetResult();
            if (pauseStop)
                await _release.Task.ConfigureAwait(false);
        }

        public void ReleaseStop() => _release.TrySetResult();
    }

    sealed class RecordingActor(List<string> order, bool failStop = false) : IActor
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
            return failStop
                ? ValueTask.FromException(new InvalidOperationException("Actor stop failed."))
                : ValueTask.CompletedTask;
        }
    }
}
