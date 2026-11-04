using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.ParameterSets.Persistence;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.State;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

/// <summary>Guards creation-based replay and non-destructive import of the historical registry.</summary>
public sealed class ParameterStartupSnapshotRepositoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Per_run_creation_replays_report_and_release_without_loading_registry(bool released)
    {
        var run = Run(Guid.NewGuid());
        var command = Address(run.RunId);
        var created = new ParameterStartupCreatedEvent { EntityId = new(run.RunId), RunId = run.RunId, Revision = 1, Run = run };
        var report = new ParameterSignalStartupReport(run.RunId, run.Plan.Fingerprint, new DateOnly(2026, 11, 3), "ES", DateTime.UtcNow, []);
        IEvent[] events = released
            ? [created, new ParameterStartupChangedEvent { EntityId = created.EntityId, RunId = run.RunId, Revision = 2, Report = report },
                new ParameterStartupChangedEvent { EntityId = created.EntityId, RunId = run.RunId, Revision = 3, Released = true }]
            : [created, new ParameterStartupChangedEvent { EntityId = created.EntityId, RunId = run.RunId, Revision = 2, Report = report }];
        var store = Substitute.For<IEventSourceActorDbContext>();
        var factory = Factory();
        store.ProviderName.Returns("System.Data.Postgres");
        store.GetEventStreamIdAsync(command.StreamId, CancellationToken.None).Returns(91L);
        store.GetCommittedStreamVersionAsync(91, CancellationToken.None).Returns(events.Length);
        Replay<ParameterStartupCommandState>(store, 91, events);

        var state = await Repository(factory, store).LoadStateAsync(command);

        Assert.Equal(run.RunId, state.RunId);
        Assert.Equal(events.Length, state.Revision);
        Assert.Equal(events.Length, state.CommittedStreamVersion);
        Assert.Equal(released, state.Released);
        Assert.Equal(!released, state.ActiveRuns.ContainsKey(run.RunId));
        Assert.Equal(!released, state.Reports.ContainsKey(run.RunId));
        Assert.Empty(state.Events);
        Assert.False(state.Updated);
        await store.DidNotReceiveWithAnyArgs().MapReduceActorEventStreamFromCreationAsync<ParameterStartupRegistryState>(
            default(long), default(Action<IEnumerable<EventStreamReadModel>>)!, default(CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Interleaved_registry_revisions_import_only_requested_run_and_keep_new_stream_uncommitted(bool released)
    {
        var run = Run(Guid.NewGuid());
        var other = Run(Guid.NewGuid());
        var command = Address(run.RunId);
        var registry = ParameterStartupEntityId.Registry;
        var legacyAddress = Address(registry.Id);
        IEvent[] events = released
            ? [Legacy(run, 1), Legacy(other, 2), new ParameterStartupChangedEvent { EntityId = registry, RunId = run.RunId, Revision = 3, Released = true }]
            : [Legacy(run, 1), Legacy(other, 2)];
        var store = Substitute.For<IEventSourceActorDbContext>();
        store.ProviderName.Returns("System.Data.Postgres");
        store.GetEventStreamIdAsync(command.StreamId, CancellationToken.None).Returns(91L);
        store.GetEventStreamIdAsync(legacyAddress.StreamId, CancellationToken.None).Returns(92L);
        store.GetCommittedStreamVersionAsync(92, CancellationToken.None).Returns(events.Length);
        Replay<ParameterStartupRegistryState>(store, 92, events);

        var state = await Repository(Factory(), store).LoadStateAsync(command);

        Assert.Equal(run.RunId, state.RunId);
        Assert.Equal(1, state.Revision);
        Assert.Equal(0, state.CommittedStreamVersion);
        Assert.Equal(released, state.Released);
        Assert.DoesNotContain(other.RunId, state.ActiveRuns.Keys);
        Assert.Empty(state.Events);
        Assert.False(state.Updated);
        Assert.NotNull(state.RegistryCreation);
        Assert.True(state.RegistryCreation.ImportedFromRegistry);
        Assert.Equal(run, state.RegistryCreation.Run);
        var result = await command.ExecuteAsync(Substitute.For<IParameterStartupCommandContext>(), state, NullLogger<ParameterStartupCommandActor>.Instance);
        Assert.True(result.Success);
        Assert.Empty(state.Events);
    }

    [Fact]
    public void A_run_stream_rejects_events_for_another_run()
    {
        var run = Run(Guid.NewGuid());
        var state = new ParameterStartupCommandState();
        Assert.True(state.Update(new ParameterStartupCreatedEvent { EntityId = new(run.RunId), RunId = run.RunId, Revision = 1, Run = run }));
        var another = Guid.NewGuid();
        Assert.Throws<InvalidDataException>(() => state.Update(new ParameterStartupChangedEvent { EntityId = new(another), RunId = another, Revision = 2, Released = true }));
        Assert.Single(state.ActiveRuns);
        Assert.Equal(1, state.Revision);
    }

    static ParameterStartupRun Run(Guid id) => new(id, [], [], new(id, "fingerprint", "fingerprint", [], null), DateTime.UtcNow, "test");

    static ApplySignalStartupPlanCommand Address(Guid id) => new()
    {
        CommandId = id, RunId = id, EntityId = new(id),
        Subject = new(ActorType.Command, ApplySignalStartupPlanCommand.Actor, ApplySignalStartupPlanCommand.Verb, id.ToString("N"))
    };

    static ParameterStartupChangedEvent Legacy(ParameterStartupRun run, long revision)
        => new() { EntityId = ParameterStartupEntityId.Registry, RunId = run.RunId, Revision = revision, Run = run };

    static IEventSourceActorStateFactory Factory()
    {
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<ParameterStartupCommandState>().Returns(_ => new ParameterStartupCommandState());
        factory.CreateState<ParameterStartupRegistryState>().Returns(_ => new ParameterStartupRegistryState());
        return factory;
    }

    static ParameterStartupStateRepository Repository(IEventSourceActorStateFactory factory, IEventSourceActorDbContext store)
        => new(factory, store, Substitute.For<IActorService>(), Substitute.For<IEventProjector<ParameterStartupCommandActor>>(), NullLogger<ParameterStartupStateRepository>.Instance);

    static void Replay<TState>(IEventSourceActorDbContext store, long stream, IEvent[] events) where TState : IActorState<TState>
    {
        if (typeof(TState) == typeof(ParameterStartupCommandState))
            store.MapReduceActorEventStreamFromCreationWithCheckpointsAsync<TState>(stream, Arg.Any<IReadOnlyList<Type>>(),
                Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), CancellationToken.None).Returns(ReplayRows);
        else
            store.MapReduceActorEventStreamFromCreationAsync<TState>(stream,
                Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), CancellationToken.None).Returns(ReplayRows);

        ValueTask ReplayRows(NSubstitute.Core.CallInfo call)
        {
            call.Arg<Action<IEnumerable<EventStreamReadModel>>>()(events.Select((source, index) => new EventStreamReadModel
            {
                EventVersion = index + 1, StreamVersion = index + 1,
                EventTypeName = source.GetType().AssemblyQualifiedName!, EventData = EventLogMessagePackCodec.Shared.Serialize(source)
            }));
            return ValueTask.CompletedTask;
        }
    }
}
