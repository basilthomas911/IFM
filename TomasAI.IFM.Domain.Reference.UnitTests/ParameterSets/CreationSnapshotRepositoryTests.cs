using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.EventModelActor;
using NSubstitute;
using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

/// <summary>Checks creation-loader identity, complete replay and explicit checkpoint fallback.</summary>
public sealed class CreationSnapshotRepositoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Creation_and_tail_restore_owner_version_and_every_fact(bool cancellable)
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellable ? cancellation.Token : CancellationToken.None;
        var source = Rows(1, 2, 3);
        var fixture = Setup(source, token);

        var state = await fixture.Repository.Load(fixture.Command, token);

        Assert.Equal(fixture.Command.Subject.ThreadId, state.Id);
        Assert.Equal(3, state.CommittedStreamVersion);
        Assert.Equal(source.Select(row => row.ToDomainEvent().Id), state.Ids);
        Assert.Empty(state.Events);
        Assert.False(state.Updated);
        await fixture.Store.Received(1).MapReduceActorEventStreamFromCreationAsync<TestState>(
            42, Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), token);
        await fixture.Store.DidNotReceiveWithAnyArgs().GetCommittedStreamVersionAsync(default, default);
    }

    [Fact]
    public async Task Incomplete_checkpoint_falls_back_to_creation_and_complete_tail()
    {
        var source = Rows(1, 2, 3);
        var fixture = Setup(source, CancellationToken.None);
        fixture.Store.MapReduceActorEventStreamFromCreationWithCheckpointAsync<TestState, ParameterAssignmentChangedEvent>(
            42, Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), CancellationToken.None).Returns(call =>
        {
            call.Arg<Action<IEnumerable<EventStreamReadModel>>>()([source[0], source[2]]);
            return ValueTask.CompletedTask;
        });

        var state = await fixture.Repository.LoadWithIncompleteCheckpoint(fixture.Command);

        Assert.Equal(3, state.Ids.Count);
        Assert.Equal(3, state.CommittedStreamVersion);
        await fixture.Store.Received(1).MapReduceActorEventStreamFromCreationAsync<TestState>(
            42, Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), CancellationToken.None);
    }

    [Fact]
    public async Task Cancelled_load_never_returns_partial_state()
    {
        var fixture = Setup([], CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await fixture.Repository.Load(fixture.Command, cancellation.Token));
        await fixture.Store.DidNotReceiveWithAnyArgs().GetEventStreamIdAsync(default!, default);
    }

    static (Repository Repository, IEventSourceActorDbContext Store, ICommand Command) Setup(
        EventStreamReadModel[] rows, CancellationToken token)
    {
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<TestState>().Returns(_ => new TestState());
        var store = Substitute.For<IEventSourceActorDbContext>();
        var command = Substitute.For<ICommand>();
        command.StreamId.Returns("Command.CreationTest.owner");
        command.Subject.Returns(new ActorSubject(ActorType.Command, "CreationTest", "Apply", "owner"));
        store.GetEventStreamIdAsync(command.StreamId, token).Returns(42L);
        store.MapReduceActorEventStreamFromCreationAsync<TestState>(42,
            Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), token).Returns(call =>
        {
            call.Arg<Action<IEnumerable<EventStreamReadModel>>>()(rows);
            return ValueTask.CompletedTask;
        });
        return (new Repository(factory, store), store, command);
    }

    static EventStreamReadModel[] Rows(params int[] versions) => versions.Select(version =>
    {
        var fact = new ParameterAssignmentChangedEvent { Id = Guid.NewGuid(), Revision = version };
        return new EventStreamReadModel { EventVersion = version, StreamVersion = version,
            EventTypeName = typeof(ParameterAssignmentChangedEvent).AssemblyQualifiedName!,
            EventData = EventLogMessagePackCodec.Shared.Serialize(fact) };
    }).ToArray();

    public sealed class TestState : BaseEventSourceActorState<TestState>
    {
        public override ActorThreadId Id { get; set; } = default!;
        public List<Guid> Ids { get; } = [];
        protected override bool Apply(IEvent fact) { Ids.Add(fact.Id); return true; }
    }

    sealed class Repository(IEventSourceActorStateFactory factory, IEventSourceActorDbContext store)
        : BaseEventSourceActorRepository(factory, store, Substitute.For<IActorService>(), NullLogger.Instance)
    {
        internal Task<TestState> Load(ICommand command, CancellationToken token)
            => LoadStateFromCreationSnapshotAsync<TestState>(command, token);
        internal Task<TestState> LoadWithIncompleteCheckpoint(ICommand command)
            => LoadStateFromCreationSnapshotWithCheckpointAsync<TestState, ParameterAssignmentChangedEvent>(command, _ => false);
        protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events)
            => ValueTask.CompletedTask;
    }
}
