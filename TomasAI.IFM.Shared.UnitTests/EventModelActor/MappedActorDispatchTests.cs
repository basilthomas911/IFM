using System;
using System.Collections.Generic;
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

public sealed class MappedActorDispatchTests
{
    [Fact]
    public void Command_receive_resolution_uses_exact_type_and_distinguishes_equal_simple_names()
    {
        var actor = CreateCommandActor();
        var first = new CollisionA.SameNameCommand();
        var second = new CollisionB.SameNameCommand();
        IReadOnlyDictionary<Type, Func<ICommand, string>> receiveMap =
            new Dictionary<Type, Func<ICommand, string>>
            {
                [typeof(CollisionA.SameNameCommand)] = static _ => "first",
                [typeof(CollisionB.SameNameCommand)] = static _ => "second"
            };

        actor.Resolve(first, receiveMap)(first).Should().Be("first");
        actor.Resolve(second, receiveMap)(second).Should().Be("second");
    }

    [Fact]
    public void Event_receive_resolution_rejects_an_unregistered_concrete_type()
    {
        var actor = CreateEventActor();
        IReadOnlyDictionary<Type, Func<IEvent, string>> receiveMap =
            new Dictionary<Type, Func<IEvent, string>>
            {
                [typeof(MappedEvent)] = static _ => "mapped"
            };

        var action = () => actor.Resolve(new UnregisteredEvent(), receiveMap);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unable to resolve MappedEvent event*");
    }

    [Fact]
    public void Event_parse_map_materializes_the_registered_verb_once()
    {
        var calls = 0;
        var actor = CreateEventActor(message =>
        {
            calls++;
            return message.AsEvent<MappedEvent>()!;
        });
        var expected = new MappedEvent();

        var actual = actor.Parse(new TestActorMessage(expected));

        actual.Should().BeSameAs(expected);
        calls.Should().Be(1);
    }

    [Theory]
    [InlineData(ActorType.Command, "MappedEvent", "Run")]
    [InlineData(ActorType.Event, "AnotherActor", "Run")]
    [InlineData(ActorType.Event, "MappedEvent", "Unknown")]
    public void Event_parse_map_ignores_unrelated_subjects(
        ActorType actorType,
        string actorName,
        string verb)
    {
        var actor = CreateEventActor();
        var message = new TestActorMessage(
            new MappedEvent(),
            new ActorSubject(actorType, actorName, verb, "entity-1"));

        actor.Parse(message).Should().BeNull();
    }

    [Fact]
    public void Event_parse_map_rejects_a_null_result_from_a_registered_parser()
    {
        var actor = CreateEventActor(_ => null!);
        var message = new TestActorMessage(new MappedEvent());

        var action = () => actor.Parse(message);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*MappedEvent.Run returned no event*");
    }

    static MappedCommandActor CreateCommandActor()
    {
        var actorId = new ActorMailboxId(ActorType.Command, MappedCommandActor.ActorName);
        return new MappedCommandActor(new MappedCommandContext(Mock.Of<IActorSupervisor>(), actorId));
    }

    static MappedEventActor CreateEventActor(Func<IActorMessage, IEvent>? parser = null)
    {
        var actorId = new ActorMailboxId(ActorType.Event, MappedEventActor.ActorName);
        return new MappedEventActor(
            new MappedEventContext(Mock.Of<IActorSupervisor>(), actorId),
            parser ?? (message => message.AsEvent<MappedEvent>()!));
    }

    sealed class MappedCommandActor(ICommandActorContext<MappedCommandActor> context)
        : BaseEventSourceCommandActor<MappedCommandActor>(
            context,
            NullLogger<MappedCommandActor>.Instance)
    {
        public const string ActorName = "MappedCommand";

        public THandler Resolve<THandler>(ICommand command, IReadOnlyDictionary<Type, THandler> receiveMap)
            where THandler : Delegate
            => ResolveMappedCommandHandler(command, receiveMap);

        protected override ICommand ParseMessage(
            ICommandActorContext<MappedCommandActor> context,
            IActorMessage message) => throw new NotSupportedException();

        protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
            ICommandActorContext<MappedCommandActor> context,
            IActorState state,
            ICommand command) => throw new NotSupportedException();

        protected override ValueTask<IActorState> OnLoadStateAsync(
            ICommandActorContext<MappedCommandActor> context,
            ActorThreadId threadId,
            ICommand command) => throw new NotSupportedException();

        protected override ValueTask OnSaveStateAsync(
            ICommandActorContext<MappedCommandActor> context,
            ActorThreadId threadId,
            IActorState state,
            ICommand command) => throw new NotSupportedException();

        protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
            ICommandActorContext<MappedCommandActor> context,
            ActorThreadId threadId,
            ICommand command,
            Exception ex) => throw new NotSupportedException();
    }

    sealed class MappedEventActor : BaseEventActor<MappedEventActor>
    {
        public const string ActorName = "MappedEvent";
        readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap;

        public MappedEventActor(
            IEventActorContext<MappedEventActor> context,
            Func<IActorMessage, IEvent> parser)
            : base(context, NullLogger<MappedEventActor>.Instance)
        {
            _parseMap = new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
            {
                ["Run"] = parser
            };
        }

        public IEvent Parse(IActorMessage message) => ParseMappedEvent(Context, message, _parseMap);

        public THandler Resolve<THandler>(IEvent @event, IReadOnlyDictionary<Type, THandler> receiveMap)
            where THandler : Delegate
            => ResolveMappedEventHandler(@event, receiveMap);

        protected override IEvent ParseMessage(
            IEventActorContext<MappedEventActor> context,
            IActorMessage message) => ParseMappedEvent(context, message, _parseMap);

        protected override ValueTask ReceiveAsync(
            IEventActorContext<MappedEventActor> context,
            IEvent @event) => ValueTask.CompletedTask;

        protected override ValueTask OnExceptionAsync(
            IEventActorContext<MappedEventActor> context,
            ActorThreadId threadId,
            IEvent @event,
            Exception ex) => ValueTask.CompletedTask;
    }

    sealed class MappedCommandContext(IActorSupervisor supervisor, ActorMailboxId actorId)
        : CommandActorContext(supervisor, actorId), ICommandActorContext<MappedCommandActor>;

    sealed class MappedEventContext(IActorSupervisor supervisor, ActorMailboxId actorId)
        : EventActorContext(supervisor, actorId), IEventActorContext<MappedEventActor>;

    sealed class TestActorMessage(IEvent @event, ActorSubject? subject = null) : IActorMessage
    {
        public ActorSubject Subject { get; } = subject ?? @event.Subject;
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => @event as TEvent;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }

    record MappedEvent : TestEventBase;
    sealed record UnregisteredEvent : TestEventBase;

    abstract record TestEventBase : IEvent
    {
        public ActorSubject Subject { get; init; } =
            new(ActorType.Event, MappedEventActor.ActorName, "Run", "entity-1");
        public Guid Id { get; init; } = Guid.NewGuid();
        public long EventId { get; init; }
        public Guid CommandId { get; init; } = Guid.NewGuid();
        public string AggregateId { get; init; } = "entity-1";
        public string EventSource { get; init; } = "unit-test";
        public DateTime ReceivedOn { get; init; } = DateTime.UtcNow;
        public string UserName => string.Empty;
        public string EventName => GetType().Name;
        public EventType EventType => EventType.DomainEvent;
    }

    static class CollisionA
    {
        internal sealed record SameNameCommand : CollisionCommandBase;
    }

    static class CollisionB
    {
        internal sealed record SameNameCommand : CollisionCommandBase;
    }

    abstract record CollisionCommandBase : ICommand
    {
        public ActorSubject Subject { get; init; } =
            new(ActorType.Command, "MappedCommand", "Run", "entity-1");
        public string CommandName => GetType().Name;
        public BoundedContextName RouteTo => BoundedContextName.Undefined;
        public Guid CommandId { get; init; } = Guid.NewGuid();
        public string StreamId => "entity-1";
        public string EventSource => "unit-test";
        public int ErrorCode => 0;
    }
}
