using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using NATS.Client.Core;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class BaseInMemoryEventSourceCommandActorTests
{
    [Fact]
    public async Task Resident_command_loads_once_and_accepts_events_after_each_commit()
    {
        var actor = CreateActor();
        var command = TestCommand.Resident("one");
        var thread = command.Subject.ThreadId;

        var first = (TestState)await actor.Load(thread, command);
        first.Update(new TestEvent());
        await actor.Save(thread, first, command);
        var second = (TestState)await actor.Load(thread, TestCommand.Resident("one"));

        second.Should().BeSameAs(first);
        actor.Loads.Should().Be(1);
        actor.Saves.Should().Be(1);
        second.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Standard_command_is_a_barrier_and_evicts_resident_state()
    {
        var actor = CreateActor();
        var resident = TestCommand.Resident("one");
        var thread = resident.Subject.ThreadId;

        await actor.Load(thread, resident);
        await actor.Load(thread, TestCommand.Standard("one"));
        await actor.Load(thread, TestCommand.Resident("one"));

        actor.Loads.Should().Be(3);
    }

    [Fact]
    public async Task Failed_commit_evicts_working_state()
    {
        var actor = CreateActor();
        var command = TestCommand.Resident("one");
        var thread = command.Subject.ThreadId;
        var state = (TestState)await actor.Load(thread, command);
        state.Update(new TestEvent());
        actor.FailNextSave = true;

        var save = async () => await actor.Save(thread, state, command);
        await save.Should().ThrowAsync<InvalidOperationException>();
        var reloaded = await actor.Load(thread, TestCommand.Resident("one"));

        reloaded.Should().NotBeSameAs(state);
        actor.Loads.Should().Be(2);
    }

    [Fact]
    public async Task Resident_state_count_is_bounded_by_activity_eviction()
    {
        var actor = CreateActor(maximumStreams: 1);
        var first = TestCommand.Resident("one");
        var second = TestCommand.Resident("two");

        await actor.Load(first.Subject.ThreadId, first);
        await actor.Load(second.Subject.ThreadId, second);
        await actor.Load(first.Subject.ThreadId, TestCommand.Resident("one"));

        actor.Loads.Should().Be(3);
        actor.Count.Should().Be(1);
    }

    [Fact]
    public async Task Deferred_reply_survives_message_disposal_and_completes_only_after_commit()
    {
        var actor = CreateActor();
        var first = new TestMessage(TestCommand.Resident("one"));
        await actor.HandleMessageAsync(first, first.Subject.ThreadId, CancellationToken.None);
        first.Reply.Should().NotBeNull();

        actor.BlockPersistence = true;
        var deferred = new TestMessage(TestCommand.Resident("one"));
        await actor.HandleMessageAsync(deferred, deferred.Subject.ThreadId, CancellationToken.None);
        deferred.Dispose();
        deferred.Reply.Should().BeNull();

        actor.ReleasePersistence();
        await deferred.Replied.Task.WaitAsync(TimeSpan.FromSeconds(1));
        deferred.Reply.Should().BeOfType<ServiceOk<GuidResult>>();
        actor.Loads.Should().Be(1);
    }

    [Fact]
    public async Task Command_window_bound_stops_admission_until_pending_commits_finish()
    {
        var actor = CreateActor(maximumCommands: 2);
        var warm = new TestMessage(TestCommand.Resident("one"));
        await actor.HandleMessageAsync(warm, warm.Subject.ThreadId, CancellationToken.None);
        actor.BlockPersistence = true;

        var second = new TestMessage(TestCommand.Resident("one"));
        var third = new TestMessage(TestCommand.Resident("one"));
        await actor.HandleMessageAsync(second, second.Subject.ThreadId, CancellationToken.None);
        var bounded = actor.HandleMessageAsync(third, third.Subject.ThreadId, CancellationToken.None).AsTask();
        await Task.Delay(20);
        bounded.IsCompleted.Should().BeFalse();

        actor.ReleasePersistence();
        await bounded.WaitAsync(TimeSpan.FromSeconds(1));
        await Task.WhenAll(second.Replied.Task, third.Replied.Task).WaitAsync(TimeSpan.FromSeconds(1));
    }

    static ResidentTestActor CreateActor(int maximumStreams = 8, int maximumCommands = 64)
    {
        var context = new Mock<ICommandActorContext<ResidentTestActor>>();
        context.SetupGet(value => value.ActorId)
            .Returns(new ActorMailboxId(ActorType.Command, "ResidentTest"));
        return new ResidentTestActor(context.Object, new InMemoryEventSourceActorOptions
        {
            OptionTradeLegDataEnabled = true,
            MaximumResidentStreams = maximumStreams,
            MaximumCommandsPerWindow = maximumCommands
        });
    }

    public sealed class ResidentTestActor(
        ICommandActorContext<ResidentTestActor> context,
        InMemoryEventSourceActorOptions options)
        : BaseInMemoryEventSourceCommandActor<ResidentTestActor, TestState>(
            context, NullLogger.Instance, options)
    {
        public int Loads { get; private set; }
        public int Saves { get; private set; }
        public int Count => ResidentStateCount;
        public bool FailNextSave { get; set; }
        public bool BlockPersistence { get; set; }
        readonly List<TaskCompletionSource> _persistence = [];

        public ValueTask<IActorState> Load(ActorThreadId id, ICommand command)
            => base.OnLoadStateAsync(Context, id, command, CancellationToken.None);
        public ValueTask Save(ActorThreadId id, IActorState state, ICommand command)
            => base.OnSaveStateAsync(Context, id, state, command, CancellationToken.None);

        protected override bool IsResidentCommand(ICommand command) => ((TestCommand)command).Hot;
        protected override bool IsResidentMessage(ActorSubject subject) => subject.Verb == "Hot";
        protected override ValueTask<TestState> LoadStateFromStoreAsync(
            ICommandActorContext<ResidentTestActor> context, ActorThreadId threadId,
            ICommand command, CancellationToken cancellationToken)
        {
            Loads++;
            return ValueTask.FromResult(new TestState { Id = threadId });
        }
        protected override ValueTask SaveStateToStoreAsync(
            ICommandActorContext<ResidentTestActor> context, ActorThreadId threadId,
            TestState state, ICommand command, CancellationToken cancellationToken)
        {
            Saves++;
            if (FailNextSave)
            {
                FailNextSave = false;
                throw new InvalidOperationException("commit failed");
            }
            return ValueTask.CompletedTask;
        }
        protected override ValueTask PersistResidentEventsAsync(
            ICommandActorContext<ResidentTestActor> context, ICommand command,
            DomainEventCollection events, long expectedStreamVersion, CancellationToken cancellationToken)
        {
            if (!BlockPersistence) return ValueTask.CompletedTask;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_persistence) _persistence.Add(completion);
            return new ValueTask(completion.Task);
        }
        public void ReleasePersistence()
        {
            TaskCompletionSource[] pending;
            lock (_persistence) { pending = _persistence.ToArray(); _persistence.Clear(); }
            foreach (var completion in pending) completion.TrySetResult();
        }
        protected override ICommand ParseMessage(ICommandActorContext<ResidentTestActor> context, IActorMessage message)
            => message.AsCommand<TestCommand>()!;
        protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
            ICommandActorContext<ResidentTestActor> context, IActorState state, ICommand command)
        {
            ((TestState)state).Update(new TestEvent { CommandId = command.CommandId });
            return ValueTask.FromResult<ServiceResult<GuidResult>>(
                new ServiceOk<GuidResult>(new GuidResult(command.CommandId)));
        }
        protected override ValueTask<ServiceResult<GuidResult>> HandleCommandExceptionAsync(
            ICommandActorContext<ResidentTestActor> context, ActorThreadId threadId,
            ICommand command, Exception exception)
            => ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(1, exception.Message));
    }

    public sealed class TestState : BaseEventSourceActorState<TestState>, IEventSourceActorState<TestState>
    {
        public override ActorThreadId Id { get; set; }
        protected override bool Apply(IEvent domainEvent) => domainEvent is TestEvent;
    }

    sealed record TestCommand : ICommand
    {
        public required ActorSubject Subject { get; init; }
        public bool Hot { get; init; }
        public string CommandName => nameof(TestCommand);
        public BoundedContextName RouteTo => BoundedContextName.OptionTradeBoundedContext;
        public Guid CommandId { get; init; } = Guid.NewGuid();
        public string StreamId => Subject.StreamId;
        public string EventSource => "Test";
        public int ErrorCode => 1;

        public static TestCommand Resident(string id) => Create(id, true, "Hot");
        public static TestCommand Standard(string id) => Create(id, false);
        static TestCommand Create(string id, bool hot, string verb = "Run") => new()
        {
            Subject = new ActorSubject(ActorType.Command, "ResidentTest", verb, id),
            Hot = hot
        };
    }

    sealed class TestMessage(TestCommand command) : IActorMessage
    {
        public ServiceResult<GuidResult>? Reply { get; private set; }
        public TaskCompletionSource Replied { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ActorSubject Subject => command.Subject;
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => command as TCommand;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => null;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => null;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class
        {
            Reply = result as ServiceResult<GuidResult>;
            Replied.TrySetResult();
            return ValueTask.CompletedTask;
        }
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }

    sealed class TestEvent : IEvent
    {
        public ActorSubject Subject { get; init; } = new(ActorType.Event, "Test", "Changed", "one");
        public Guid Id { get; init; } = Guid.NewGuid();
        public Guid CommandId { get; init; }
        public long EventId { get; init; }
        public string AggregateId { get; init; } = "one";
        public string EventSource { get; init; } = "Test";
        public DateTime ReceivedOn { get; init; } = DateTime.UtcNow;
        public string UserName => "test";
        public string EventName => nameof(TestEvent);
        public EventType EventType => EventType.DomainEvent;
    }
}
