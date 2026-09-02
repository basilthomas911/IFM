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
    public void Event_receive_resolution_returns_a_no_op_for_an_unregistered_concrete_type()
    {
        var actor = CreateEventActor();
        IReadOnlyDictionary<Type, Func<IEvent, string>> receiveMap =
            new Dictionary<Type, Func<IEvent, string>>
            {
                [typeof(MappedEvent)] = static _ => "mapped"
            };

        actor.Resolve(new UnregisteredEvent(), receiveMap)(new UnregisteredEvent())
            .Should().BeNull();
    }

    [Fact]
    public async Task Event_receive_resolution_returns_a_completed_task_no_op()
    {
        var actor = CreateEventActor();
        IReadOnlyDictionary<Type, Func<IEvent, Task>> receiveMap =
            new Dictionary<Type, Func<IEvent, Task>>();

        var task = actor.Resolve(new UnregisteredEvent(), receiveMap)(new UnregisteredEvent());

        task.Should().BeSameAs(Task.CompletedTask);
        await task;
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

    [Fact]
    public void Realtime_parse_map_materializes_the_registered_verb_once_without_requiring_a_command_id()
    {
        var calls = 0;
        var expected = new MappedRealtimeEvent
        {
            Subject = RealtimeSubject(),
            CommandId = Guid.Empty
        };
        var actor = CreateRealtimeActor(message =>
        {
            calls++;
            return message.AsEvent<MappedRealtimeEvent>()!;
        });

        var actual = actor.Parse(new TestActorMessage(expected));

        actual.Should().BeSameAs(expected);
        calls.Should().Be(1);
    }

    [Theory]
    [InlineData(ActorType.Event, "MappedRealtime", "Run")]
    [InlineData(ActorType.Realtime, "AnotherRealtimeActor", "Run")]
    [InlineData(ActorType.Realtime, "MappedRealtime", "Unknown")]
    public void Realtime_parse_map_ignores_unrelated_subjects(
        ActorType actorType,
        string actorName,
        string verb)
    {
        var actor = CreateRealtimeActor();
        var @event = new MappedRealtimeEvent { Subject = RealtimeSubject() };
        var message = new TestActorMessage(
            @event,
            new ActorSubject(actorType, actorName, verb, "entity-1"));

        actor.Parse(message).Should().BeNull();
    }

    [Fact]
    public void Realtime_parse_map_rejects_a_null_result_from_a_registered_parser()
    {
        var actor = CreateRealtimeActor(_ => null!);
        var message = new TestActorMessage(
            new MappedRealtimeEvent { Subject = RealtimeSubject() });

        var action = () => actor.Parse(message);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*MappedRealtime.Run returned no realtime event*");
    }

    [Fact]
    public async Task Realtime_parse_failure_is_logged_without_calling_a_null_event_exception_handler()
    {
        var actor = CreateRealtimeActor(_ => throw new FormatException("bad payload"));
        var message = new TestActorMessage(
            new MappedRealtimeEvent { Subject = RealtimeSubject() });

        await actor.HandleMessageAsync(message, message.Subject.ThreadId);

        actor.ExecutionExceptionCalls.Should().Be(0);
        message.ReleaseCalls.Should().Be(1);
    }

    [Fact]
    public void Realtime_receive_resolution_uses_exact_concrete_type()
    {
        var actor = CreateRealtimeActor();
        var first = new RealtimeCollisionA.SameNameEvent { Subject = RealtimeSubject() };
        var second = new RealtimeCollisionB.SameNameEvent { Subject = RealtimeSubject() };
        IReadOnlyDictionary<Type, Func<IEvent, string>> receiveMap =
            new Dictionary<Type, Func<IEvent, string>>
            {
                [typeof(RealtimeCollisionA.SameNameEvent)] = static _ => "first",
                [typeof(RealtimeCollisionB.SameNameEvent)] = static _ => "second"
            };

        actor.Resolve(first, receiveMap)(first).Should().Be("first");
        actor.Resolve(second, receiveMap)(second).Should().Be("second");
    }

    [Fact]
    public void Realtime_receive_resolution_returns_a_no_op_for_an_unregistered_concrete_type()
    {
        var actor = CreateRealtimeActor();
        IReadOnlyDictionary<Type, Func<IEvent, ValueTask>> receiveMap =
            new Dictionary<Type, Func<IEvent, ValueTask>>();

        var result = actor.Resolve(new UnregisteredEvent(), receiveMap)(new UnregisteredEvent());

        result.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void Query_receive_resolution_uses_exact_concrete_type()
    {
        var actor = CreateQueryActor();
        var first = new QueryCollisionA.SameNameQuery();
        var second = new QueryCollisionB.SameNameQuery();
        IReadOnlyDictionary<Type, Func<IQuery, string>> receiveMap =
            new Dictionary<Type, Func<IQuery, string>>
            {
                [typeof(QueryCollisionA.SameNameQuery)] = static _ => "first",
                [typeof(QueryCollisionB.SameNameQuery)] = static _ => "second"
            };

        actor.Resolve(first, receiveMap)(first).Should().Be("first");
        actor.Resolve(second, receiveMap)(second).Should().Be("second");
    }

    [Fact]
    public void Query_parse_map_materializes_and_registers_the_query_once()
    {
        var calls = 0;
        var expected = new MappedQuery();
        var actor = CreateQueryActor(message =>
        {
            calls++;
            return message.AsQuery<MappedQuery, MappedQueryResult>()!;
        });
        var message = new TestQueryActorMessage(expected);

        var actual = actor.Parse(message);

        actual.Should().BeSameAs(expected);
        calls.Should().Be(1);
        actor.QueryContext.GetMessageInfo(expected.Subject.ThreadId, expected.Subject.Verb)
            .Should().NotBeNull();
    }

    [Fact]
    public void Query_parse_map_wraps_malformed_deserialization_consistently()
    {
        var actor = CreateQueryActor(_ => throw new ArgumentNullException("payload"));

        var action = () => actor.Parse(new TestQueryActorMessage(new MappedQuery()));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unable to deserialize MappedQuery.Run query*")
            .WithInnerException<ArgumentNullException>();
    }

    [Fact]
    public async Task Query_parse_failure_replies_without_calling_a_null_query_exception_handler()
    {
        var actor = CreateQueryActor(_ => throw new FormatException("bad payload"));
        var message = new TestQueryActorMessage(new MappedQuery());

        await actor.HandleMessageAsync(message, message.Subject.ThreadId);

        actor.ExecutionExceptionCalls.Should().Be(0);
        message.Reply.Should().BeOfType<ServiceFailed<object>>()
            .Which.ErrorCode.Should().Be(9998);
    }

    [Fact]
    public async Task Query_exception_map_preserves_the_declared_result_contract()
    {
        var actor = CreateQueryActor();
        var query = new MappedQuery();
        var message = new TestQueryActorMessage(query);
        actor.Parse(message);

        await actor.FailAsync(query, new InvalidOperationException("failed"));

        var failure = message.Reply.Should().BeOfType<ServiceFailed<MappedQueryResult>>().Subject;
        failure.ErrorCode.Should().Be(query.ErrorCode);
        failure.ErrorMessage.Should().Be("failed");
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

    static MappedQueryActor CreateQueryActor(Func<IActorMessage, IQuery>? parser = null)
    {
        var actorId = new ActorMailboxId(ActorType.Query, MappedQueryActor.ActorName);
        return new MappedQueryActor(
            new MappedQueryContext(Mock.Of<IActorSupervisor>(), actorId),
            parser ?? (message => message.AsQuery<MappedQuery, MappedQueryResult>()!));
    }

    static MappedRealtimeActor CreateRealtimeActor(Func<IActorMessage, IEvent>? parser = null)
    {
        var actorId = new ActorMailboxId(ActorType.Realtime, MappedRealtimeActor.ActorName);
        return new MappedRealtimeActor(
            new MappedRealtimeContext(Mock.Of<IActorSupervisor>(), actorId),
            parser ?? (message => message.AsEvent<MappedRealtimeEvent>()!));
    }

    static ActorSubject RealtimeSubject() =>
        new(ActorType.Realtime, MappedRealtimeActor.ActorName, "Run", "entity-1");

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

    sealed class MappedRealtimeActor : BaseEventActor<MappedRealtimeActor>
    {
        public const string ActorName = "MappedRealtime";
        readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap;

        public MappedRealtimeActor(
            IRealtimeActorContext<MappedRealtimeActor> context,
            Func<IActorMessage, IEvent> parser)
            : base(context, NullLogger<MappedRealtimeActor>.Instance)
        {
            _parseMap = new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
            {
                ["Run"] = parser
            };
        }

        public int ExecutionExceptionCalls { get; private set; }

        public IEvent Parse(IActorMessage message) =>
            ParseMappedRealtimeEvent(Context, message, _parseMap);

        public THandler Resolve<THandler>(IEvent @event, IReadOnlyDictionary<Type, THandler> receiveMap)
            where THandler : Delegate
            => ResolveMappedEventHandler(@event, receiveMap);

        protected override IEvent ParseMessage(
            IEventActorContext<MappedRealtimeActor> context,
            IActorMessage message) => ParseMappedRealtimeEvent(context, message, _parseMap);

        protected override ValueTask ReceiveAsync(
            IEventActorContext<MappedRealtimeActor> context,
            IEvent @event) => ValueTask.CompletedTask;

        protected override ValueTask OnExceptionAsync(
            IEventActorContext<MappedRealtimeActor> context,
            ActorThreadId threadId,
            IEvent @event,
            Exception ex)
        {
            ExecutionExceptionCalls++;
            return ValueTask.CompletedTask;
        }
    }

    sealed class MappedQueryActor : BaseQueryActor<MappedQueryActor>
    {
        public const string ActorName = "MappedQuery";
        readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap;
        static readonly IReadOnlyDictionary<Type, Func<IQuery, string>> _receiveMap =
            new Dictionary<Type, Func<IQuery, string>>
            {
                [typeof(MappedQuery)] = static _ => "mapped"
            };
        static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
            CreateQueryExceptionMap(_receiveMap.Keys);

        public MappedQueryActor(
            MappedQueryContext context,
            Func<IActorMessage, IQuery> parser)
            : base(context, NullLogger<MappedQueryActor>.Instance)
        {
            QueryContext = context;
            _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
            {
                ["Run"] = parser
            };
        }

        public MappedQueryContext QueryContext { get; }
        public int ExecutionExceptionCalls { get; private set; }

        public IQuery Parse(IActorMessage message) => ParseMappedQuery(Context, message, _parseMap);

        public THandler Resolve<THandler>(IQuery query, IReadOnlyDictionary<Type, THandler> receiveMap)
            where THandler : Delegate
            => ResolveMappedQueryHandler(query, receiveMap);

        public ValueTask FailAsync(IQuery query, Exception exception)
            => ExceptionMappedQueryAsync(
                Context,
                query.Subject.ThreadId,
                query,
                query.Subject.Verb,
                exception,
                _exceptionMap);

        protected override IQuery ParseMessage(
            IQueryActorContext<MappedQueryActor> context,
            IActorMessage message) => ParseMappedQuery(context, message, _parseMap);

        protected override ValueTask ReceiveAsync(
            IQueryActorContext<MappedQueryActor> context,
            IQuery query) => ValueTask.CompletedTask;

        protected override ValueTask OnExceptionAsync(
            IQueryActorContext<MappedQueryActor> context,
            ActorThreadId threadId,
            IQuery query,
            string verb,
            Exception ex)
        {
            ExecutionExceptionCalls++;
            return ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
        }
    }

    sealed class MappedCommandContext(IActorSupervisor supervisor, ActorMailboxId actorId)
        : CommandActorContext(supervisor, actorId), ICommandActorContext<MappedCommandActor>;

    sealed class MappedEventContext(IActorSupervisor supervisor, ActorMailboxId actorId)
        : EventActorContext(supervisor, actorId), IEventActorContext<MappedEventActor>;

    sealed class MappedRealtimeContext(IActorSupervisor supervisor, ActorMailboxId actorId)
        : EventActorContext(supervisor, actorId), IRealtimeActorContext<MappedRealtimeActor>;

    sealed class MappedQueryContext(IActorSupervisor supervisor, ActorMailboxId actorId)
        : QueryActorContext(supervisor, actorId), IQueryActorContext<MappedQueryActor>;

    sealed class TestActorMessage(IEvent @event, ActorSubject? subject = null) : IActorMessage
    {
        public int ReleaseCalls { get; private set; }
        public ActorSubject Subject { get; } = subject ?? @event.Subject;
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => @event as TEvent;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() => ReleaseCalls++;
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }

    sealed class TestQueryActorMessage(IQuery query, ActorSubject? subject = null) : IActorMessage
    {
        public bool CanReply => true;
        public object? Reply { get; private set; }
        public ActorSubject Subject { get; } = subject ?? query.Subject;
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class
            => query as TQuery;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class
        {
            Reply = result;
            return ValueTask.CompletedTask;
        }
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }

    sealed record MappedQueryResult;

    record MappedQuery : TestQueryBase<MappedQueryResult>;

    abstract record TestQueryBase<TResult> : IQuery<TResult> where TResult : class
    {
        public ActorSubject Subject { get; init; } =
            new(ActorType.Query, MappedQueryActor.ActorName, "Run", "entity-1");
        public IActorEntityId EntityId { get; init; } = new ActorEntityId("entity-1");
        public int ErrorCode { get; init; } = 47001;
        public string? QueryParams { get; init; }
    }

    static class QueryCollisionA
    {
        internal sealed record SameNameQuery : TestQueryBase<MappedQueryResult>;
    }

    static class QueryCollisionB
    {
        internal sealed record SameNameQuery : TestQueryBase<MappedQueryResult>;
    }

    record MappedEvent : TestEventBase;
    sealed record UnregisteredEvent : TestEventBase;
    sealed record MappedRealtimeEvent : TestEventBase;

    static class RealtimeCollisionA
    {
        internal sealed record SameNameEvent : TestEventBase;
    }

    static class RealtimeCollisionB
    {
        internal sealed record SameNameEvent : TestEventBase;
    }

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
