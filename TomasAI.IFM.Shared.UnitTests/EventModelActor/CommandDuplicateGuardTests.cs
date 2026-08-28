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

public sealed class CommandAuditLoggerTests
{
    [Fact]
    public async Task Duplicate_is_acknowledged_without_validation_execution_or_persistence()
    {
        var auditLogger = new SequencedAuditLogger(true, false);
        var actorId = new ActorMailboxId(ActorType.Command, TestCommandActor.ActorName);
        var supervisor = CreateSupervisor(actorId, auditLogger);
        var actor = new TestCommandActor(
            new TestCommandContext(supervisor.Object, actorId));
        var command = new TestCommand();
        var acceptedMessage = new TestCommandMessage(command);
        var duplicateMessage = new TestCommandMessage(command);

        await actor.StartAsync(supervisor.Object);
        await actor.HandleMessageAsync(acceptedMessage);
        await actor.HandleMessageAsync(duplicateMessage);
        await actor.StopAsync();

        auditLogger.Calls.Should().Be(2);
        actor.Validations.Should().Be(1);
        actor.StateLoads.Should().Be(1);
        actor.Executions.Should().Be(1);
        actor.StateSaves.Should().Be(1);
        acceptedMessage.Reply.Should().NotBeNull();
        duplicateMessage.Reply.Should().NotBeNull();
        duplicateMessage.Reply!.Success.Should().BeTrue();
        duplicateMessage.Reply.Value!.Guid.Should().Be(command.CommandId);
    }

    [Fact]
    public async Task Audit_failure_prevents_validation_execution_and_persistence()
    {
        var auditLogger = new ThrowingAuditLogger();
        var actorId = new ActorMailboxId(ActorType.Command, TestCommandActor.ActorName);
        var supervisor = CreateSupervisor(actorId, auditLogger);
        var actor = new TestCommandActor(new TestCommandContext(supervisor.Object, actorId));
        var message = new TestCommandMessage(new TestCommand());

        await actor.StartAsync(supervisor.Object);
        await actor.HandleMessageAsync(message);
        await actor.StopAsync();

        auditLogger.Calls.Should().Be(1);
        actor.Validations.Should().Be(0);
        actor.StateLoads.Should().Be(0);
        actor.Executions.Should().Be(0);
        actor.StateSaves.Should().Be(0);
        message.Reply.Should().NotBeNull();
        message.Reply!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Unresolvable_message_does_not_reach_the_audit_logger()
    {
        var auditLogger = new SequencedAuditLogger(true);
        var actorId = new ActorMailboxId(ActorType.Command, TestCommandActor.ActorName);
        var supervisor = CreateSupervisor(actorId, auditLogger);
        var actor = new TestCommandActor(new TestCommandContext(supervisor.Object, actorId));
        var command = new TestCommand
        {
            Subject = new ActorSubject(ActorType.Query, TestCommandActor.ActorName, "Run", "entity-1")
        };
        var message = new TestCommandMessage(command);

        await actor.StartAsync(supervisor.Object);
        await actor.HandleMessageAsync(message);
        await actor.StopAsync();

        auditLogger.Calls.Should().Be(0);
        actor.Executions.Should().Be(0);
        message.Reply.Should().NotBeNull();
        message.Reply!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Empty_command_id_is_rejected_before_audit_reservation()
    {
        var auditLogger = new SequencedAuditLogger(true);
        var actorId = new ActorMailboxId(ActorType.Command, TestCommandActor.ActorName);
        var supervisor = CreateSupervisor(actorId, auditLogger);
        var actor = new TestCommandActor(new TestCommandContext(supervisor.Object, actorId));
        var message = new TestCommandMessage(new TestCommand { CommandId = Guid.Empty });

        await actor.StartAsync(supervisor.Object);
        await actor.HandleMessageAsync(message);
        await actor.StopAsync();

        auditLogger.Calls.Should().Be(0);
        actor.Validations.Should().Be(0);
        actor.StateLoads.Should().Be(0);
        actor.Executions.Should().Be(0);
        actor.StateSaves.Should().Be(0);
        message.Reply.Should().NotBeNull();
        message.Reply!.Success.Should().BeFalse();
        message.Reply.ErrorMessage.Should().Contain("CommandId is empty");
    }

    static Mock<IActorSupervisor> CreateSupervisor(
        ActorMailboxId actorId,
        ICommandAuditLogger auditLogger)
    {
        var container = new ContainerInstance(type => type == typeof(ICommandAuditLogger)
            ? auditLogger
            : throw new InvalidOperationException($"Unexpected service request: {type}"));
        var producer = new Mock<IActorProducer>();
        var supervisor = new Mock<IActorSupervisor>();
        supervisor.SetupGet(instance => instance.Container).Returns(container);
        supervisor.Setup(instance => instance.CreateMailbox(actorId)).Returns(Mock.Of<IActorMailbox>());
        supervisor.Setup(instance => instance.GetProducer(actorId)).Returns(producer.Object);
        return supervisor;
    }

    sealed class SequencedAuditLogger(params bool[] decisions) : ICommandAuditLogger
    {
        readonly Queue<bool> _decisions = new(decisions);
        public int Calls { get; private set; }

        public ValueTask<CommandAuditReservation> TryReserveAsync(
            ICommand command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return ValueTask.FromResult(new CommandAuditReservation(_decisions.Dequeue()));
        }
    }

    sealed class ThrowingAuditLogger : ICommandAuditLogger
    {
        public int Calls { get; private set; }

        public ValueTask<CommandAuditReservation> TryReserveAsync(
            ICommand command,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("Audit unavailable.");
        }
    }

    sealed class TestCommandActor(ICommandActorContext<TestCommandActor> actorContext)
        : BaseEventSourceCommandActor<TestCommandActor>(
            actorContext,
            NullLogger<TestCommandActor>.Instance)
    {
        public const string ActorName = "DuplicateGuardTest";

        static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> ParseMap =
            new Dictionary<string, Func<IActorMessage, ICommand>>
            {
                ["Run"] = message => message.AsCommand<TestCommand>()!
            };

        public int Validations { get; private set; }
        public int StateLoads { get; private set; }
        public int Executions { get; private set; }
        public int StateSaves { get; private set; }

        protected override ICommand ParseMessage(
            ICommandActorContext<TestCommandActor> context,
            IActorMessage message)
            => ParseMappedCommand(context, message, ParseMap);

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
