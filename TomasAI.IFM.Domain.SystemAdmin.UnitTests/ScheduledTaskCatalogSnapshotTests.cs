using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;

public sealed class ScheduledTaskCatalogSnapshotTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Every_replay_entry_point_accepts_initial_registration_snapshot_and_keeps_revision_fences(int entryPoint)
    {
        var id = new ScheduledTaskId(Guid.NewGuid());
        var snapshot = new ScheduledTaskCatalog { Id = id, Revision = 8, Projects = [new() { TaskKey = "existing" }] };
        var observed = snapshot with { Revision = 9, HostCapability = new() { Ready = true } };
        IEvent[] events = [new ScheduledTaskCatalogProjectRegisteredEvent { EntityId = id, ScheduledTaskCatalog = snapshot },
            new ScheduledTaskCatalogHostCapabilityRecordedEvent { EntityId = id, ScheduledTaskCatalog = observed }];
        var state = new ScheduledTaskCatalogCommandState();
        switch (entryPoint)
        {
            case 0: state.ReplayEvents(new DomainEventCollection(events)); break;
            case 1: state.ReplayEvents((IEnumerable<IEvent>)events); break;
            case 2: state.ReplayEvents(Rows(events).ToArray()); break;
            case 3: state.ReplayEvents(Rows(events)); break;
        }
        Assert.Equivalent(observed, state.ScheduledTaskCatalog); Assert.Empty(state.Events); Assert.False(state.Updated);
        Assert.False(state.Apply(new ScheduledTaskCatalogProjectRegisteredEvent { EntityId = id, ScheduledTaskCatalog = snapshot with { Revision = 11 } }));
        Assert.False(state.Apply(new ScheduledTaskCatalogHostCapabilityRecordedEvent { EntityId = id, ScheduledTaskCatalog = snapshot }));
        var other = new ScheduledTaskId(Guid.NewGuid());
        Assert.False(state.Apply(new ScheduledTaskCatalogProjectRegisteredEvent { EntityId = other, ScheduledTaskCatalog = snapshot with { Id = other, Revision = 10 } }));
        Assert.Equivalent(observed, state.ScheduledTaskCatalog); Assert.Empty(state.Events);
        Assert.True(state.Apply(new ScheduledTaskCatalogProjectRegisteredEvent { EntityId = id, ScheduledTaskCatalog = observed with { Revision = 10 } }));
        Assert.Single(state.Events);
    }

    [Fact]
    public void Live_updates_and_capability_replay_cannot_seed_a_later_revision()
    {
        var id = new ScheduledTaskId(Guid.NewGuid());
        var state = new ScheduledTaskCatalogCommandState();
        var later = new ScheduledTaskCatalog { Id = id, Revision = 8 };
        Assert.False(state.Apply(new ScheduledTaskCatalogProjectRegisteredEvent { EntityId = id, ScheduledTaskCatalog = later }));
        state.ReplayEvents(new IEvent[] { new ScheduledTaskCatalogHostCapabilityRecordedEvent { EntityId = id, ScheduledTaskCatalog = later } });
        Assert.Null(state.ScheduledTaskCatalog); Assert.Empty(state.Events); Assert.False(state.Updated);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Invalid_snapshot_revision_or_identity_is_rejected(long revision)
    {
        var id = new ScheduledTaskId(Guid.NewGuid());
        var state = new ScheduledTaskCatalogCommandState();
        state.ReplayEvents(new IEvent[] { new ScheduledTaskCatalogProjectRegisteredEvent { EntityId = id, ScheduledTaskCatalog = new() { Id = id, Revision = revision } },
            new ScheduledTaskCatalogProjectRegisteredEvent { EntityId = id, ScheduledTaskCatalog = new() { Id = new(Guid.NewGuid()), Revision = 8 } } });
        Assert.Null(state.ScheduledTaskCatalog); Assert.Empty(state.Events); Assert.False(state.Updated);
    }

    private static IEnumerable<EventStreamReadModel> Rows(IEnumerable<IEvent> events) => events.Select((source, index) => new EventStreamReadModel
    {
        EventVersion = index + 8, StreamVersion = index + 8, EventTypeName = source.GetType().AssemblyQualifiedName!,
        EventData = TomasAI.IFM.Application.EventSourcing.EventLogMessagePackCodec.Shared.Serialize(source)
    });
}
