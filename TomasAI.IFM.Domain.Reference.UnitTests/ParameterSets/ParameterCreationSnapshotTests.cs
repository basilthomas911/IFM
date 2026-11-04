using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

/// <summary>Creation snapshots must preserve their wire identity and replay every later fact.</summary>
public sealed class ParameterCreationSnapshotTests
{
    [Fact]
    public void Assignment_creation_retains_inherited_numeric_payload_and_concrete_event_identity()
    {
        var source = new ParameterAssignmentCreatedEvent
        {
            CommandId = Guid.NewGuid(), EntityId = new(Guid.NewGuid()), Revision = 1,
            RequestHash = "original-request"
        };
        var restored = MessagePackSerializer.Deserialize<ParameterAssignmentCreatedEvent>(MessagePackSerializer.Serialize(source));

        Assert.Equal(source, restored);
        Assert.Equal(nameof(ParameterAssignmentCreatedEvent), restored.EventName);
        Assert.IsType<ParameterAssignmentCreatedEvent>(EventLogMessagePackCodec.Shared.Deserialize(
            typeof(ParameterAssignmentCreatedEvent).AssemblyQualifiedName!, 1, EventLogMessagePackCodec.Shared.Serialize(source)));
    }

    [Fact]
    public void Startup_creation_retains_run_identity_and_concrete_event_identity()
    {
        var runId = Guid.NewGuid();
        var source = new ParameterStartupCreatedEvent
        {
            CommandId = runId, EntityId = new(runId), RunId = runId, Revision = 1
        };
        var restored = MessagePackSerializer.Deserialize<ParameterStartupCreatedEvent>(MessagePackSerializer.Serialize(source));

        Assert.Equal(source, restored);
        Assert.Equal(nameof(ParameterStartupCreatedEvent), restored.EventName);
        Assert.IsType<ParameterStartupCreatedEvent>(EventLogMessagePackCodec.Shared.Deserialize(
            typeof(ParameterStartupCreatedEvent).AssemblyQualifiedName!, 1, EventLogMessagePackCodec.Shared.Serialize(source)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Set_replays_from_creation_preserving_earlier_versions_and_duplicate_receipts(bool cancellable)
    {
        var id = Guid.NewGuid();
        var command = new CreateParameterSetCommand
        {
            EntityId = new(id), Subject = new(ActorType.Command, CreateParameterSetCommand.Actor, CreateParameterSetCommand.Verb, id.ToString("N"))
        };
        var values = new RegimeDiscoveryParameterModel().CreateTypedDraft(id).WithIdentity(id, 1);
        var first = new ParameterSetVersion(new(id, 1, values.ComponentCode, ParameterValueCodec.Hash(values)), "First", "",
            values.SchemaVersion, ParameterVersionStatus.Draft, "", DateTime.UtcNow, "test", CatalogRevision: 1) { Parameters = values };
        var secondValues = values.WithIdentity(id, 2);
        var second = first with { Reference = first.Reference with { Version = 2, PayloadSha256 = ParameterValueCodec.Hash(secondValues) },
            Parameters = secondValues, CatalogRevision = 2 };
        var created = new ParameterSetCreatedEvent { CommandId = Guid.NewGuid(), EntityId = new(id), Revision = 1, Version = first, RequestHash = "create" };
        var saved = new ParameterDraftSavedEvent { CommandId = Guid.NewGuid(), EntityId = new(id), Revision = 2, Version = second, RequestHash = "save" };
        using var cancellation = new CancellationTokenSource();
        var token = cancellable ? cancellation.Token : CancellationToken.None;
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<ParameterSetCommandState>().Returns(new ParameterSetCommandState());
        var store = Substitute.For<IEventSourceActorDbContext>();
        store.ProviderName.Returns("System.Data.Postgres");
        store.GetEventStreamIdAsync(command.StreamId, token).Returns(91L);
        store.GetCommittedStreamVersionAsync(91, token).Returns(2L);
        store.MapReduceActorEventStreamFromCreationAsync<ParameterSetCommandState>(91,
            Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), token).Returns(call =>
        {
            call.Arg<Action<IEnumerable<EventStreamReadModel>>>()(Rows(created, saved));
            return ValueTask.CompletedTask;
        });
        var repository = new ParameterSetStateRepository(factory, store, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<ParameterSetCommandActor>>(), NullLogger<ParameterSetStateRepository>.Instance);

        var state = cancellable ? await repository.LoadStateAsync(command, token) : await repository.LoadStateAsync(command);

        Assert.Equal(2, state.CatalogRevision);
        Assert.Equal(2, state.Versions.Count);
        Assert.Equal(first.Reference, state.Versions[1].Reference);
        Assert.Equal(second.Reference, state.Versions[2].Reference);
        Assert.Equal("create", state.Operations[created.CommandId]);
        Assert.Equal("save", state.Operations[saved.CommandId]);
        Assert.Equal(2, state.Receipts.Count);
        Assert.Equal(command.Subject.ThreadId, state.Id);
        Assert.Equal(2, state.CommittedStreamVersion);
        Assert.Empty(state.Events);
        Assert.False(state.Updated);
        await store.DidNotReceiveWithAnyArgs().MapReduceActorEventStreamAsync<ParameterSetCommandState>(default, default!, default);
    }

    static IEnumerable<EventStreamReadModel> Rows(params IEvent[] events)
        => events.Select((source, index) => new EventStreamReadModel
        {
            EventVersion = index + 1, StreamVersion = index + 1,
            EventTypeName = source.GetType().AssemblyQualifiedName!,
            EventData = EventLogMessagePackCodec.Shared.Serialize(source)
        });
}
