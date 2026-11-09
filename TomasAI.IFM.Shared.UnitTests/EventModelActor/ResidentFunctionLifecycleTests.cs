using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class ResidentFunctionLifecycleTests
{
    [Fact]
    public async Task Success_retains_only_committed_state_and_reuses_it_for_the_next_request()
    {
        var repository = new Repository();
        var actor = new Actor(repository);
        Assert.True((await Run(actor, new Request())).Success);
        Assert.True((await Run(actor, new Request { Value = 2 })).Success);
        Assert.Equal(1, repository.Loads);
        Assert.Equal(2, repository.Saves);
        Assert.Equal(2, repository.Version);
        Assert.Equal(1, actor.ResidentCacheStatistics.Hits);
    }

    [Fact]
    public async Task Duplicate_completion_is_replayed_and_conflicting_payload_is_rejected()
    {
        var repository = new Repository();
        var actor = new Actor(repository);
        var request = new Request();
        Assert.True((await Run(actor, request)).Success);
        Assert.True((await Run(actor, request)).Success);
        Assert.False((await Run(actor, request with { Value = 42 })).Success);
        Assert.Equal(1, repository.Saves);
        Assert.Equal(2, repository.ReplayChecks);
    }

    [Fact]
    public async Task A_mutable_result_cannot_modify_the_private_committed_checkpoint()
    {
        var repository = new Repository();
        var actor = new Actor(repository);
        var request = new Request { Value = 7 };
        var first = await Run(actor, request);
        first.Value!.Completed!.Values[0] = 999;
        var replay = await Run(actor, request);
        Assert.Equal(7, replay.Value!.Completed!.Values[0]);
        Assert.Equal(1, repository.Loads);
    }

    [Fact]
    public async Task Failed_calculation_is_not_cached_as_a_terminal_result()
    {
        var repository = new Repository();
        var actor = new Actor(repository) { FailCalculation = true };
        var request = new Request();
        Assert.False((await Run(actor, request)).Success);
        Assert.Equal(0, repository.Saves);
        actor.FailCalculation = false;
        Assert.True((await Run(actor, request)).Success);
        Assert.Equal(2, repository.Loads);
        Assert.Equal(1, repository.Saves);
    }

    [Fact]
    public async Task Failed_persistence_evicts_the_working_state_and_the_retry_loads_again()
    {
        var repository = new Repository { FailSave = true };
        var actor = new Actor(repository);
        var request = new Request();
        Assert.False((await Run(actor, request)).Success);
        Assert.Equal(0, repository.Version);
        repository.FailSave = false;
        Assert.True((await Run(actor, request)).Success);
        Assert.Equal(2, repository.Loads);
        Assert.Equal(1, repository.Version);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Timeout_or_caller_cancellation_fences_a_late_commit_and_the_next_request(bool cancelCaller)
    {
        var repository = new Repository();
        var actor = new Actor(repository);
        Assert.True((await Run(actor, new Request())).Success);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.SaveGate = gate.Task;
        repository.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        actor.Deadline = cancelCaller ? DateTime.UtcNow.AddSeconds(20) : DateTime.UtcNow.AddMilliseconds(150);
        var failed = Run(actor, new Request { Value = 2 }, cancellation.Token);
        await repository.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        if (cancelCaller)
        {
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => failed);
        }
        else Assert.False((await failed.WaitAsync(TimeSpan.FromSeconds(3))).Success);
        Assert.Equal(1, actor.ResidentCacheStatistics.PinnedEntries);
        actor.Deadline = null;
        var next = Run(actor, new Request { Value = 3 });
        Assert.False(next.IsCompleted);
        repository.SaveGate = null;
        gate.SetResult();
        Assert.True((await next.WaitAsync(TimeSpan.FromSeconds(3))).Success);
        Assert.Equal(3, repository.Version);
        Assert.Equal(2, repository.Loads);
        Assert.Equal(3, repository.Latest!.Values[0]);
    }

    [Fact]
    public async Task A_load_that_acquires_after_timeout_cannot_attach_to_the_next_invocation_or_leak_a_pin()
    {
        var repository = new Repository();
        var actor = new Actor(repository);
        Assert.True((await Run(actor, new Request())).Success);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        actor.LoadEntryGate = gate.Task;
        actor.Deadline = DateTime.UtcNow.AddMilliseconds(150);
        var abandoned = Run(actor, new Request { Value = 2 });
        await actor.DelayedLoadEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False((await abandoned.WaitAsync(TimeSpan.FromSeconds(3))).Success);
        actor.Deadline = null;
        actor.LoadEntryGate = null;
        Assert.True((await Run(actor, new Request { Value = 3 })).Success);
        gate.SetResult();
        await actor.DelayedLoadFinished.Task.WaitAsync(TimeSpan.FromSeconds(3));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (actor.ResidentCacheStatistics.PinnedEntries != 0) await Task.Delay(1, deadline.Token);
        Assert.Equal(0, actor.ResidentCacheStatistics.Entries);
        Assert.True((await Run(actor, new Request { Value = 4 })).Success);
        Assert.Equal(2, repository.Loads);
        Assert.Equal(3, repository.Version);
        Assert.Equal(4, repository.Latest!.Values[0]);
    }

    [Fact]
    public async Task Disabled_policy_keeps_the_original_load_for_each_request()
    {
        var repository = new Repository();
        var actor = new Actor(repository, enabled: false);
        Assert.True((await Run(actor, new Request())).Success);
        Assert.True((await Run(actor, new Request { Value = 2 })).Success);
        Assert.Equal(2, repository.Loads);
        Assert.Equal(0, actor.ResidentCacheStatistics.Entries);
    }

    static async Task<ServiceResult<FunctionResult<Completed, Failed>>> Run(Actor actor, Request request, CancellationToken token = default)
    {
        var message = new Message(request);
        await actor.HandleMessageAsync(message, request.Subject.ThreadId, token);
        return message.Reply!;
    }

    sealed class Actor(Repository repository, bool enabled = true)
        : BaseInMemoryEventSourceFunctionActor<Actor, Request, ActorEntityId, ActorEntityId, State, Completed, Failed>(
            new Context(), repository, null, NullLogger.Instance, new ResidentActorPolicy { Enabled = enabled })
    {
        public Task? LoadEntryGate { get; set; }
        public TaskCompletionSource DelayedLoadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DelayedLoadFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async ValueTask<State> LoadFunctionStateAsync(Request request, CancellationToken token)
        {
            if (LoadEntryGate is not { } gate) return await base.LoadFunctionStateAsync(request, token);
            DelayedLoadEntered.TrySetResult();
            // A non-cooperative late load can enter after its caller stopped waiting.
            try { await gate; return await base.LoadFunctionStateAsync(request, CancellationToken.None); }
            finally { DelayedLoadFinished.TrySetResult(); }
        }
        public bool FailCalculation { get; set; }
        public DateTime? Deadline { get; set; }
        protected override FunctionExecutionPolicy ResolveExecutionPolicy(Request request, FunctionFailureStage stage)
            => new(TimeProvider.System, Deadline, FunctionCompletionMode.EventThenProjection);
        protected override Request ParseMessage(IFunctionActorContext<Actor> context, IActorMessage message) => message.AsCommand<Request>()!;
        protected override ValueTask<FunctionResult<Completed, Failed>> ExecuteFunctionAsync(IFunctionActorContext<Actor> context,
            State state, Request request, CancellationToken token)
            => ValueTask.FromResult(FailCalculation ? FunctionResult<Completed, Failed>.Fail(Failure(request, "calculation"))
                : FunctionResult<Completed, Failed>.Complete(new Completed
                { CommandId = request.CommandId, EntityId = request.EntityId, Subject = request.Subject, Values = [request.Value] }));
        protected override Failed CreateConflictFailedEvent(Request request) => Failure(request, "conflict");
        protected override Failed CreateFailedEvent(Request? request, Exception exception, FunctionFailureStage stage)
            => Failure(request, stage + ": " + exception.Message);
        protected override ResidentFunctionCheckpoint<Request, State>? CaptureCommittedState(State state) => state.Capture();
        static Failed Failure(Request? request, string error) => new()
        { CommandId = request?.CommandId ?? Guid.Empty, EntityId = request?.EntityId ?? new("test"), ErrorMessage = error };
    }

    sealed class Context : IFunctionActorContext<Actor>
    {
        public ActorMailboxId ActorId => new(ActorType.Function, "ResidentFunctionTest");
        public IContainerInstance Container => null!;
    }

    sealed class Repository : IResidentFunctionStateRepository<State, Request>
    {
        public int Loads, Saves, ReplayChecks;
        public long Version;
        public Completed? Latest;
        public bool FailSave;
        public Task? SaveGate;
        public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<State> LoadStateAsync(Request request, CancellationToken cancellationToken = default)
        {
            Loads++;
            var state = new State();
            if (Latest is not null) state.ReplayEvents(new IEvent[] { Latest });
            state.RestoreCommittedStreamVersion(Version);
            return ValueTask.FromResult(state.Prepare(request));
        }
        public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, State state, Request request, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            if (SaveGate is { } gate) await gate;
            if (FailSave) throw new InvalidOperationException("save failed");
            Assert.Equal(Version, state.CommittedStreamVersion);
            Latest = state.Latest;
            Version++;
            Saves++;
            state.AcceptChanges();
        }
        public ValueTask<bool> IsCurrentAsync(State state, Request request, CancellationToken cancellationToken)
        { ReplayChecks++; return ValueTask.FromResult(state.CommittedStreamVersion == Version); }
    }

    public sealed class State : BaseEventSourceActorState<State>, IEventSourceFunctionState<State, Request, Completed>
    {
        Guid active;
        public override ActorThreadId Id { get; set; } = default!;
        public Completed? Latest { get; private set; }
        public Completed? CompletedEvent => Latest?.CommandId == active ? Latest : null;
        public bool IsCompleted => CompletedEvent is not null;
        public State Prepare(Request request) { active = request.CommandId; Id = request.Subject.ThreadId; return this; }
        public bool Matches(Request request) => CompletedEvent?.Values[0] == request.Value;
        public bool TryComplete(Completed completed, Request request) => !IsCompleted && Update(completed, request);
        protected override bool Apply(IEvent source)
        { if (source is not Completed completed) return false; Latest = completed; return true; }
        public ResidentFunctionCheckpoint<Request, State>? Capture()
        {
            if (Latest is null || CommittedStreamVersion == 0 || Events.Count != 0) return null;
            return ResidentFunctionCheckpoint<Request, State>.Capture(Latest, CommittedStreamVersion,
                static (request, fact, version) =>
                {
                    var state = new State();
                    state.ReplayEvents(new IEvent[] { fact });
                    state.RestoreCommittedStreamVersion(version);
                    return state.Prepare(request);
                });
        }
    }

    public sealed record Request : ICommand<ActorEntityId>
    {
        public ActorSubject Subject { get; init; } = new(ActorType.Function, "ResidentFunctionTest", "Execute", "one");
        public ActorEntityId EntityId { get; init; } = new("one");
        public Guid CommandId { get; init; } = Guid.NewGuid();
        public int Value { get; init; } = 1;
        public string CommandName => nameof(Request);
        public BoundedContextName RouteTo => default;
        public string StreamId => Subject.StreamId;
        public string EventSource => "resident-test";
        public int ErrorCode => 1;
    }

    public sealed record Completed : ICompleteEvent<ActorEntityId>
    {
        public ActorSubject Subject { get; init; }
        public Guid Id { get; init; } = Guid.NewGuid();
        public long EventId { get; init; }
        public Guid CommandId { get; init; }
        public ActorEntityId EntityId { get; init; }
        public string AggregateId { get; init; } = string.Empty;
        public string EventSource { get; init; } = "resident-test";
        public DateTime ReceivedOn { get; init; } = DateTime.UtcNow;
        public int[] Values { get; init; } = [];
        public string UserName => "test";
        public string EventName => nameof(Completed);
        public EventType EventType => EventType.CompletedEvent;
    }

    public sealed record Failed : IErrorEvent<ActorEntityId>
    {
        public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
        public Guid Id { get; init; }
        public long EventId { get; init; }
        public Guid CommandId { get; init; }
        public ActorEntityId EntityId { get; init; }
        public string AggregateId { get; init; } = string.Empty;
        public string EventSource { get; init; } = "resident-test";
        public DateTime ReceivedOn { get; init; }
        public DateTime ErrorDate { get; init; }
        public int ErrorCode { get; init; } = 1;
        public string ErrorMessage { get; init; } = string.Empty;
        public ErrorType ErrorType { get; init; }
        public string ErrorData { get; init; } = string.Empty;
        public string CommandName { get; init; } = "test";
        public string CommandData { get; init; } = string.Empty;
        public string UserName => "test";
        public string EventName => nameof(Failed);
        public EventType EventType => EventType.ErrorEvent;
    }

    sealed class Message(Request request) : IActorMessage
    {
        public ServiceResult<FunctionResult<Completed, Failed>>? Reply;
        public ActorSubject Subject => request.Subject;
        public ActorSubject ReplySubject { get; set; } = default!;
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => request as TCommand;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class
        { Reply = (ServiceResult<FunctionResult<Completed, Failed>>)(object)result; return ValueTask.CompletedTask; }
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }
}
