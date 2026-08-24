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

public sealed class CommandDuplicateGuardTests
{
    [Fact]
    public async Task Duplicate_is_acknowledged_without_validation_execution_or_persistence()
    {
        var guard = new SequencedGuard(true, false);
        var actorId = new ActorMailboxId(ActorType.Command, TestCommandActor.ActorName);
        var supervisor = CreateSupervisor(actorId, guard);
        var actor = new TestCommandActor(
            new TestCommandContext(supervisor.Object, actorId));
        var command = new TestCommand();
        var acceptedMessage = new TestCommandMessage(command);
        var duplicateMessage = new TestCommandMessage(command);

        await actor.StartAsync(supervisor.Object);
        await actor.HandleMessageAsync(acceptedMessage);
        await actor.HandleMessageAsync(duplicateMessage);
        await actor.StopAsync();

        guard.Calls.Should().Be(2);
        actor.Validations.Should().Be(1);
        actor.StateLoads.Should().Be(1);
        actor.Executions.Should().Be(1);
        actor.StateSaves.Should().Be(1);
        acceptedMessage.Reply.Should().NotBeNull();
        duplicateMessage.Reply.Should().NotBeNull();
        duplicateMessage.Reply!.Success.Should().BeTrue();
        duplicateMessage.Reply.Value!.Guid.Should().Be(command.CommandId);
    }

    static Mock<IActorSupervisor> CreateSupervisor(
        ActorMailboxId actorId,
        ICommandDuplicateGuard guard)
    {
        var container = new ContainerInstance(type => type == typeof(ICommandDuplicateGuard)
            ? guard
            : throw new InvalidOperationException($"Unexpected service request: {type}"));
        var producer = new Mock<IActorProducer>();
        var supervisor = new Mock<IActorSupervisor>();
        supervisor.SetupGet(instance => instance.Container).Returns(container);
        supervisor.Setup(instance => instance.CreateMailbox(actorId)).Returns(Mock.Of<IActorMailbox>());
        supervisor.Setup(instance => instance.GetProducer(actorId)).Returns(producer.Object);
        return supervisor;
    }

    sealed class SequencedGuard(params bool[] decisions) : ICommandDuplicateGuard
    {
        readonly Queue<bool> _decisions = new(decisions);
        public int Calls { get; private set; }

        public ValueTask<bool> TryAcceptAsync(
            ICommand command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return ValueTask.FromResult(_decisions.Dequeue());
        }
    }

    sealed class TestCommandActor(ICommandActorContext<TestCommandActor> actorContext)
        : BaseEventSourceCommandActor<TestCommandActor>(
            actorContext,
            NullLogger<TestCommandActor>.Instance)
    {
        public const string ActorName = "DuplicateGuardTest";

        public int Validations { get; private set; }
        public int StateLoads { get; private set; }
        public int Executions { get; private set; }
        public int StateSaves { get; private set; }

        protected override ICommand ParseMessage(ICommandActorContext<TestCommandActor> context, IActorMessage message)
            => message.AsCommand<TestCommand>()!;

        protected override ValueTask OnValidateAsync(
            ICommandActorContext<TestCommandActor> context,
            ActorThreadId threadId,
            ICommand command)
        {
            Validations++;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask<IActorState> OnLoadStateAsync(
            ICommandActorContext<TestCommandActor> context,
            ActorThreadId threadId,
            ICommand command)
        {
            StateLoads++;
            return ValueTask.FromResult<IActorState>(new TestState());
        }

        protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
            ICommandActorContext<TestCommandActor> context,
            IActorState state,
            ICommand command)
        {
            Executions++;
            return ValueTask.FromResult<ServiceResult<GuidResult>>(
                new ServiceOk<GuidResult>(new GuidResult(command.CommandId)));
        }

        protected override ValueTask OnSaveStateAsync(
            ICommandActorContext<TestCommandActor> context,
            ActorThreadId threadId,
            IActorState state,
            ICommand command)
        {
            StateSaves++;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
            ICommandActorContext<TestCommandActor> context,
            ActorThreadId threadId,
            ICommand command,
            Exception ex)
            => ValueTask.FromResult<ServiceResult<GuidResult>>(
                new ServiceFailed<GuidResult>(-1, ex.Message));
    }

    sealed class TestCommandContext(IActorSupervisor supervisor, ActorMailboxId actorId)
        : CommandActorContext(supervisor, actorId), ICommandActorContext<TestCommandActor>
    {
    }

    sealed class TestState : IActorState
    {
        public ActorThreadId Id { get; set; }
    }

    sealed record TestCommand : ICommand
    {
        public ActorSubject Subject { get; init; } =
            new(ActorType.Command, "DuplicateGuardTest", "Run", "entity-1");
        public string CommandName => nameof(TestCommand);
        public BoundedContextName RouteTo => default;
        public Guid CommandId { get; init; } = Guid.NewGuid();
        public string StreamId => "entity-1";
        public string EventSource => "unit-test";
        public int ErrorCode => 0;
    }

    sealed class TestCommandMessage(TestCommand command) : IActorMessage
    {
        public ServiceResult<GuidResult>? Reply { get; private set; }
        public ActorSubject Subject => command.Subject;
        public ActorSubject ReplySubject { get; set; }

        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand
            => command as TCommand;

        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;

        public TQuery? AsQuery<TQuery, TResult>()
            where TQuery : class, IQuery<TResult>
            where TResult : class => default;

        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class
        {
            Reply = result as ServiceResult<GuidResult>;
            return ValueTask.CompletedTask;
        }

        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }
}
