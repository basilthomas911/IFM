using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Validation;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class FunctionActorLifecycleTests
{
    [Fact]
    public async Task Completed_result_is_projected_then_saved_and_returned()
    {
        var calls = new List<string>();
        var repository = new TestRepository(new TestState(), calls);
        var projector = new TestProjector(calls);
        var actor = new TestFunctionActor(repository, projector, (_, request) =>
            FunctionResult<TestCompletedEvent, TestFailedEvent>.Complete(Completed(request)));
        var message = new TestMessage(new TestRequest());

        await actor.HandleMessageAsync(message);

        calls.Should().Equal("project", "save");
        actor.Executions.Should().Be(1);
        message.Reply.Should().NotBeNull();
        message.Reply!.Success.Should().BeTrue();
        message.Reply.Value!.Completed.Should().NotBeNull();
    }

    [Fact]
    public async Task Failed_result_is_returned_without_projection_or_save()
    {
        var calls = new List<string>();
        var repository = new TestRepository(new TestState(), calls);
        var projector = new TestProjector(calls);
        var actor = new TestFunctionActor(repository, projector, (_, request) =>
            FunctionResult<TestCompletedEvent, TestFailedEvent>.Fail(Failed(request, "calculation")));
        var message = new TestMessage(new TestRequest());

        await actor.HandleMessageAsync(message);

        calls.Should().BeEmpty();
        message.Reply.Should().NotBeNull();
        message.Reply!.Success.Should().BeFalse();
        message.Reply.Value!.Failed!.ErrorMessage.Should().Be("calculation");
    }

    [Fact]
    public async Task Completed_result_can_commit_without_an_optional_projector()
    {
        var calls = new List<string>();
        var repository = new TestRepository(new TestState(), calls);
        var actor = new TestFunctionActor(repository, null, (_, request) =>
            FunctionResult<TestCompletedEvent, TestFailedEvent>.Complete(Completed(request)));
        var message = new TestMessage(new TestRequest());

        await actor.HandleMessageAsync(message);

        calls.Should().Equal("save");
        message.Reply!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Invalid_non_terminal_result_becomes_failure_without_side_effects()
    {
        var calls = new List<string>();
        var actor = new TestFunctionActor(
            new TestRepository(new TestState(), calls),
            new TestProjector(calls),
            (_, _) => new FunctionResult<TestCompletedEvent, TestFailedEvent>());
        var message = new TestMessage(new TestRequest());

        await actor.HandleMessageAsync(message);

        calls.Should().BeEmpty();
        message.Reply!.Success.Should().BeFalse();
        message.Reply.Value!.Failed!.ErrorMessage.Should().Contain("exactly one");
    }

    [Fact]
    public async Task Projector_exception_becomes_failed_result_and_prevents_save()
    {
        var calls = new List<string>();
        var repository = new TestRepository(new TestState(), calls);
        var projector = new TestProjector(calls, new InvalidOperationException("projection unavailable"));
        var actor = new TestFunctionActor(repository, projector, (_, request) =>
            FunctionResult<TestCompletedEvent, TestFailedEvent>.Complete(Completed(request)));
        var message = new TestMessage(new TestRequest());

        await actor.HandleMessageAsync(message);

        calls.Should().Equal("project");
        message.Reply.Should().NotBeNull();
        message.Reply!.Success.Should().BeFalse();
        message.Reply.Value!.Failed!.ErrorMessage.Should().Contain("Projection");
    }

    [Fact]
    public async Task Persistence_exception_becomes_failed_result_after_projection()
    {
        var calls = new List<string>();
        var repository = new TestRepository(
            new TestState(),
            calls,
            new InvalidOperationException("event store unavailable"));
        var actor = new TestFunctionActor(repository, new TestProjector(calls), (_, request) =>
            FunctionResult<TestCompletedEvent, TestFailedEvent>.Complete(Completed(request)));
        var message = new TestMessage(new TestRequest());

        await actor.HandleMessageAsync(message);

        calls.Should().Equal("project", "save");
        message.Reply!.Success.Should().BeFalse();
        message.Reply.Value!.Failed!.ErrorMessage.Should().Contain("Persistence");
    }

    [Fact]
    public async Task Existing_matching_completion_is_returned_without_execution_projection_or_save()
    {
        var request = new TestRequest();
        var completed = Completed(request);
        var state = new TestState();
        state.TryComplete(completed, request).Should().BeTrue();
        var calls = new List<string>();
        var repository = new TestRepository(state, calls);
        var projector = new TestProjector(calls);
        var actor = new TestFunctionActor(repository, projector, (_, _) =>
            throw new InvalidOperationException("must not execute"));
        var message = new TestMessage(request);

        await actor.HandleMessageAsync(message);

        calls.Should().BeEmpty();
        actor.Executions.Should().Be(0);
        message.Reply!.Success.Should().BeTrue();
        message.Reply.Value!.Completed.Should().BeSameAs(completed);
    }

    [Fact]
    public async Task Existing_completion_for_different_request_returns_conflict_without_side_effects()
    {
        var original = new TestRequest();
        var state = new TestState();
        state.TryComplete(Completed(original), original).Should().BeTrue();
        var calls = new List<string>();
        var actor = new TestFunctionActor(
            new TestRepository(state, calls),
            new TestProjector(calls),
            (_, _) => throw new InvalidOperationException("must not execute"));
        var message = new TestMessage(new TestRequest { CommandId = Guid.NewGuid() });

        await actor.HandleMessageAsync(message);

        calls.Should().BeEmpty();
        actor.Executions.Should().Be(0);
        message.Reply!.Success.Should().BeFalse();
        message.Reply.Value!.Failed!.ErrorMessage.Should().Be("conflict");
    }

    [Fact]
    public void Mapped_validation_aggregates_errors_and_preserves_command_error_code()
    {
        var actor = ValidationActor();
        actor.ValidationMap = new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(TestRequest)] = _ => [new("first"), new("second")]
        };
        Action validate = () => actor.ValidateRequest(new TestRequest());
        var error = validate.Should().Throw<CommandValidationException>().Which;
        error.ErrorCode.Should().Be(0);
        error.Message.Should().Be($"first{Environment.NewLine}second{Environment.NewLine}");
    }

    [Fact]
    public void Mapped_validation_rejects_unmapped_types_and_null_error_collections()
    {
        var actor = ValidationActor();
        actor.ValidationMap = new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(ICommand)] = _ => []
        };
        Action validate = () => actor.ValidateRequest(new TestRequest());
        validate.Should().Throw<InvalidOperationException>().WithMessage("Unable to validate*");
        actor.ValidationMap = new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(TestRequest)] = _ => null!
        };
        validate.Should().Throw<InvalidOperationException>().WithMessage("*returned no error collection*");
    }

    [Fact]
    public void Mapped_validation_checks_null_arguments()
    {
        var actor = ValidationActor();
        Action missingCommand = () => actor.ValidateRequest(null!);
        missingCommand.Should().Throw<ArgumentNullException>();
        actor.ValidationMap = null!;
        Action missingMap = () => actor.ValidateRequest(new TestRequest());
        missingMap.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Invalid_mapped_request_is_rejected_before_load_execute_project_or_save()
    {
        var calls = new List<string>();
        var repository = new TestRepository(new TestState(), calls);
        var actor = new TestFunctionActor(repository, new TestProjector(calls), (_, request) =>
            FunctionResult<TestCompletedEvent, TestFailedEvent>.Complete(Completed(request)))
        {
            ValidationMap = new Dictionary<Type, Func<ICommand, List<ValidationError>>>
            {
                [typeof(TestRequest)] = _ => [new("invalid request")]
            }
        };
        var message = new TestMessage(new TestRequest());
        await actor.HandleMessageAsync(message);
        repository.Loads.Should().Be(0);
        actor.Executions.Should().Be(0);
        calls.Should().BeEmpty();
        message.Reply!.Value!.Failed!.ErrorMessage.Should().Contain("invalid request");
    }

    [Fact]
    public void Event_map_dispatches_only_the_exact_requested_event_handler()
    {
        var request = new TestRequest();
        var invoked = 0;
        var map = new Dictionary<Type, Func<FunctionEventContext<TestRequest>, TimeProvider, FunctionResult<TestCompletedEvent, TestFailedEvent>>>
        {
            [typeof(TestCompletedEvent)] = (input, _) =>
            {
                invoked++;
                return FunctionResult<TestCompletedEvent, TestFailedEvent>.Complete(Completed(input.Request!));
            },
            [typeof(TestFailedEvent)] = (_, _) => throw new InvalidOperationException("wrong handler")
        };
        var result = TestFunctionActor.DispatchEvent(new(typeof(TestCompletedEvent), request), map);
        result.Completed!.CommandId.Should().Be(request.CommandId);
        invoked.Should().Be(1);
        Action unmapped = () => TestFunctionActor.DispatchEvent(new(typeof(IEvent), request), map);
        unmapped.Should().Throw<InvalidOperationException>().WithMessage("*exact event type*");
        invoked.Should().Be(1);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("both")]
    [InlineData("wrong")]
    public void Event_map_rejects_invalid_or_wrong_kind_terminal_results(string scenario)
    {
        var request = new TestRequest();
        var map = new Dictionary<Type, Func<FunctionEventContext<TestRequest>, TimeProvider, FunctionResult<TestCompletedEvent, TestFailedEvent>>>
        {
            [typeof(TestCompletedEvent)] = (_, _) => scenario switch
            {
                "null" => null!,
                "empty" => new(),
                "both" => new() { Completed = Completed(request), Failed = Failed(request, "failed") },
                _ => FunctionResult<TestCompletedEvent, TestFailedEvent>.Fail(Failed(request, "failed"))
            }
        };
        Action dispatch = () => TestFunctionActor.DispatchEvent(new(typeof(TestCompletedEvent), request), map);
        dispatch.Should().Throw<InvalidOperationException>().WithMessage("*incompatible terminal result*");
    }

    [Fact]
    public async Task Policy_is_resolved_for_each_stage_and_completed_observation_does_not_repeat_on_replay()
    {
        var request = new TestRequest(); var calls = new List<string>(); var state = new TestState();
        var actor = new TestFunctionActor(new TestRepository(state, calls), new TestProjector(calls),
            (_, c) => FunctionResult<TestCompletedEvent, TestFailedEvent>.Complete(Completed(c)));
        var stages = new List<FunctionFailureStage>();
        actor.Policy = (_, stage) => { stages.Add(stage); return new(TimeProvider.System, null); };
        await actor.HandleMessageAsync(new TestMessage(request));
        await actor.HandleMessageAsync(new TestMessage(request));
        stages.Should().Equal(FunctionFailureStage.Loading, FunctionFailureStage.Execution, FunctionFailureStage.Projection,
            FunctionFailureStage.Persistence, FunctionFailureStage.Loading);
        actor.Observations.Should().Equal(FunctionEventPhase.Committed, FunctionEventPhase.Replayed);
        actor.Executions.Should().Be(1); calls.Should().Equal("project", "save");
        await actor.HandleMessageAsync(new TestMessage(request with { CommandId = Guid.NewGuid() }));
        actor.Observations.Should().HaveCount(2, "a conflicting replay does not observe a successful completion");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Observer_failure_cannot_replace_a_durable_completion_or_replay(bool replay)
    {
        var request = new TestRequest(); var state = new TestState(); var completed = Completed(request);
        if (replay) state.TryComplete(completed, request);
        var actor = new TestFunctionActor(new TestRepository(state, []), null,
            (_, _) => FunctionResult<TestCompletedEvent, TestFailedEvent>.Complete(completed)) { FailObservation = true };
        var message = new TestMessage(request); await actor.HandleMessageAsync(message);
        message.Reply!.Value!.Completed.Should().BeSameAs(completed);
        actor.Observations.Should().Equal(replay ? FunctionEventPhase.Replayed : FunctionEventPhase.Committed);
    }

    [Fact]
    public async Task Expired_loading_policy_prevents_loading_or_executing()
    {
        var repo = new TestRepository(new TestState(), []);
        var actor = new TestFunctionActor(repo, null, (_, request) => FunctionResult<TestCompletedEvent, TestFailedEvent>.Complete(Completed(request)));
        actor.Policy = (_, _) => new(TimeProvider.System, DateTime.UtcNow.AddMinutes(-1));
        var message = new TestMessage(new TestRequest()); await actor.HandleMessageAsync(message);
        repo.Loads.Should().Be(0); actor.Executions.Should().Be(0);
        message.Reply!.Value!.IsFailed.Should().BeTrue(); actor.Observations.Should().BeEmpty();
    }

    [Fact]
    public void Policy_rejects_null_clock_and_non_utc_or_default_deadlines()
    {
        FluentActions.Invoking(() => new FunctionExecutionPolicy(null!, null)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new FunctionExecutionPolicy(TimeProvider.System, DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified))).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new FunctionExecutionPolicy(TimeProvider.System, default(DateTime))).Should().Throw<ArgumentException>();
        new FunctionExecutionPolicy(TimeProvider.System, null).DeadlineUtc.Should().BeNull();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("stage")]
    public void Policy_dispatch_rejects_missing_exact_key_null_policy_and_nonexecution_stages(string scenario)
    {
        var map = new Dictionary<Type, Func<TestRequest, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>>
        { [scenario == "missing" ? typeof(ICommand) : typeof(TestRequest)] = (_, _, clock) => scenario == "null" ? null! : new(clock, null) };
        Action act = () => TestFunctionActor.DispatchPolicy(new TestRequest(),
            scenario == "stage" ? FunctionFailureStage.Validation : FunctionFailureStage.Execution, map);
        act.Should().Throw<Exception>();
    }

    static TestFunctionActor ValidationActor() => new(
        new TestRepository(new TestState(), []), null, (_, request) =>
            FunctionResult<TestCompletedEvent, TestFailedEvent>.Complete(Completed(request)));

    static TestCompletedEvent Completed(TestRequest request) => new()
    {
        Subject = request.Subject,
        Id = Guid.NewGuid(),
        EntityId = request.EntityId,
        CommandId = request.CommandId,
        AggregateId = request.StreamId,
        EventSource = request.EventSource,
        ReceivedOn = DateTime.UtcNow
    };

    static TestFailedEvent Failed(TestRequest? request, string message) => new()
    {
        Subject = request?.Subject ?? ActorSubject.Unknown,
        Id = Guid.NewGuid(),
        EntityId = request?.EntityId ?? ActorEntityId.Default,
        CommandId = request?.CommandId ?? Guid.Empty,
        AggregateId = request?.StreamId ?? string.Empty,
        EventSource = request?.EventSource ?? "unit-test",
        ReceivedOn = DateTime.UtcNow,
        ErrorDate = DateTime.UtcNow,
        ErrorCode = -1,
        ErrorMessage = message,
        ErrorType = ErrorType.Command,
        CommandName = request?.CommandName ?? nameof(TestRequest)
    };

    sealed class TestFunctionActor(
        TestRepository repository,
        TestProjector? projector,
        Func<TestState, TestRequest, FunctionResult<TestCompletedEvent, TestFailedEvent>> execute)
        : BaseEventSourceFunctionActor<
            TestFunctionActor,
            TestRequest,
            ActorEntityId,
            ActorEntityId,
            TestState,
            TestCompletedEvent,
            TestFailedEvent>(
                new TestContext(),
                repository,
                projector,
                NullLogger<TestFunctionActor>.Instance)
    {
        public IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> ValidationMap { get; set; } =
            new Dictionary<Type, Func<ICommand, List<ValidationError>>> { [typeof(TestRequest)] = _ => [] };

        public void ValidateRequest(TestRequest request) => ValidateMappedCommand(request, ValidationMap);

        protected override ValueTask ValidateAsync(IFunctionActorContext<TestFunctionActor> context,
            ActorThreadId threadId, TestRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateRequest(request);
            return ValueTask.CompletedTask;
        }

        public static FunctionResult<TestCompletedEvent, TestFailedEvent> DispatchEvent(
            FunctionEventContext<TestRequest> input,
            IReadOnlyDictionary<Type, Func<FunctionEventContext<TestRequest>, TimeProvider, FunctionResult<TestCompletedEvent, TestFailedEvent>>> map)
            => DispatchMappedFunctionEvent(input, TimeProvider.System, map);

        public Func<TestRequest, FunctionFailureStage, FunctionExecutionPolicy> Policy { get; set; } = (_, _) => new(TimeProvider.System, null);
        static readonly IReadOnlyDictionary<Type, Func<TestRequest, FunctionFailureStage, TestFunctionActor, FunctionExecutionPolicy>> _executionPolicyMap =
            new Dictionary<Type, Func<TestRequest, FunctionFailureStage, TestFunctionActor, FunctionExecutionPolicy>>
            { [typeof(TestRequest)] = (request, stage, actor) => actor.Policy(request, stage) };
        protected override FunctionExecutionPolicy ResolveExecutionPolicy(TestRequest request, FunctionFailureStage stage)
            => DispatchMappedExecutionPolicy(request, stage, this, _executionPolicyMap);
        public List<FunctionEventPhase> Observations { get; } = [];
        public bool FailObservation { get; set; }
        protected override FunctionResult<TestCompletedEvent, TestFailedEvent> HandleFunctionEvent(
            IFunctionActorContext<TestFunctionActor> context, FunctionEventContext<TestRequest> input)
        {
            if (input.Phase != FunctionEventPhase.Outcome)
            {
                Observations.Add(input.Phase);
                if (FailObservation) throw new InvalidOperationException("Injected observer failure");
            }
            return base.HandleFunctionEvent(context, input);
        }

        public static FunctionExecutionPolicy DispatchPolicy(TestRequest request, FunctionFailureStage stage,
            IReadOnlyDictionary<Type, Func<TestRequest, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>> map)
            => DispatchMappedExecutionPolicy(request, stage, TimeProvider.System, map);

        public int Executions { get; private set; }

        protected override TestRequest ParseMessage(
            IFunctionActorContext<TestFunctionActor> context,
            IActorMessage message) => message.AsCommand<TestRequest>()!;

        protected override ValueTask<FunctionResult<TestCompletedEvent, TestFailedEvent>> ExecuteFunctionAsync(
            IFunctionActorContext<TestFunctionActor> context,
            TestState state,
            TestRequest request,
            CancellationToken cancellationToken)
        {
            Executions++;
            return ValueTask.FromResult(execute(state, request));
        }

        protected override TestFailedEvent CreateConflictFailedEvent(TestRequest request)
            => Failed(request, "conflict");

        protected override TestFailedEvent CreateFailedEvent(
            TestRequest? request,
            Exception exception,
            FunctionFailureStage stage)
            => Failed(request, $"{stage}: {exception.Message}");
    }

    sealed class TestContext : IFunctionActorContext<TestFunctionActor>
    {
        public ActorMailboxId ActorId { get; } = new(ActorType.Function, "TestFunction");
        public IContainerInstance Container => throw new NotSupportedException();
    }

    sealed class TestRepository(TestState state, List<string> calls, Exception? exception = null)
        : IEventSourceFunctionStateRepository<TestState, TestRequest>
    {
        public int Loads { get; private set; }
        public ValueTask<TestState> LoadStateAsync(
            TestRequest request,
            CancellationToken cancellationToken = default)
        {
            Loads++;
            return ValueTask.FromResult(state);
        }

        public ValueTask SaveCompletedStateAsync(
            IFunctionActorContext context,
            TestState stateToSave,
            TestRequest request,
            CancellationToken cancellationToken = default)
        {
            calls.Add("save");
            return exception is null
                ? ValueTask.CompletedTask
                : ValueTask.FromException(exception);
        }
    }

    sealed class TestProjector(List<string> calls, Exception? exception = null)
        : IFunctionProjector<TestCompletedEvent>
    {
        public ValueTask ProjectAsync(
            TestCompletedEvent completedEvent,
            CancellationToken cancellationToken = default)
        {
            calls.Add("project");
            return exception is null
                ? ValueTask.CompletedTask
                : ValueTask.FromException(exception);
        }
    }

    sealed class TestState
        : BaseEventSourceActorState<TestState>,
          IEventSourceFunctionState<TestState, TestRequest, TestCompletedEvent>
    {
        public override ActorThreadId Id { get; set; }
        public TestCompletedEvent? CompletedEvent { get; private set; }
        public bool IsCompleted => CompletedEvent is not null;

        public bool Matches(TestRequest request) => CompletedEvent?.CommandId == request.CommandId;
        public bool TryComplete(TestCompletedEvent completedEvent, TestRequest request)
            => !IsCompleted && Update(completedEvent, request);

        protected override bool Apply(IEvent domainEvent)
        {
            if (domainEvent is not TestCompletedEvent completed)
                return false;
            CompletedEvent = completed;
            return true;
        }
    }

    sealed record TestRequest : ICommand<ActorEntityId>
    {
        public ActorSubject Subject { get; init; } = new(ActorType.Function, "TestFunction", "Execute", "entity-1");
        public string CommandName => nameof(TestRequest);
        public BoundedContextName RouteTo => default;
        public Guid CommandId { get; init; } = Guid.NewGuid();
        public ActorEntityId EntityId { get; init; } = new("entity-1");
        public string StreamId => Subject.StreamId;
        public string EventSource => "unit-test";
        public int ErrorCode => 0;
    }

    sealed record TestCompletedEvent : ICompleteEvent<ActorEntityId>
    {
        public ActorSubject Subject { get; init; }
        public Guid Id { get; init; }
        public long EventId { get; init; }
        public Guid CommandId { get; init; }
        public ActorEntityId EntityId { get; init; }
        public string AggregateId { get; init; } = string.Empty;
        public string EventSource { get; init; } = string.Empty;
        public DateTime ReceivedOn { get; init; }
        public string UserName => "UnitTest";
        public string EventName => nameof(TestCompletedEvent);
        public EventType EventType => EventType.CompletedEvent;
    }

    sealed record TestFailedEvent : IErrorEvent<ActorEntityId>
    {
        public ActorSubject Subject { get; init; }
        public Guid Id { get; init; }
        public long EventId { get; init; }
        public Guid CommandId { get; init; }
        public ActorEntityId EntityId { get; init; }
        public string AggregateId { get; init; } = string.Empty;
        public string EventSource { get; init; } = string.Empty;
        public DateTime ReceivedOn { get; init; }
        public DateTime ErrorDate { get; init; }
        public int ErrorCode { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;
        public ErrorType ErrorType { get; init; }
        public string ErrorData { get; init; } = string.Empty;
        public string CommandName { get; init; } = string.Empty;
        public string CommandData { get; init; } = string.Empty;
        public string UserName => "UnitTest";
        public string EventName => nameof(TestFailedEvent);
        public EventType EventType => EventType.ErrorEvent;
    }

    sealed class TestMessage(TestRequest request) : IActorMessage
    {
        public ServiceResult<FunctionResult<TestCompletedEvent, TestFailedEvent>>? Reply { get; private set; }
        public ActorSubject Subject => request.Subject;
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => request as TCommand;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class
        {
            Reply = result as ServiceResult<FunctionResult<TestCompletedEvent, TestFailedEvent>>;
            return ValueTask.CompletedTask;
        }
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }
}
