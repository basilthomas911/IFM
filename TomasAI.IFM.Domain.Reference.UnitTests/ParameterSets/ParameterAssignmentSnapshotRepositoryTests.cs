using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

public sealed class ParameterAssignmentSnapshotRepositoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Creation_and_legacy_replay_preserve_disable_audit_and_duplicate_evidence(bool legacy)
    {
        var id = Guid.NewGuid();
        var values = new RegimeDiscoveryParameterModel().CreateTypedDraft(id).WithIdentity(id, 1);
        var version = new ParameterSetVersion(new(id, 1, values.ComponentCode, ParameterValueCodec.Hash(values)), "Published", "",
            values.SchemaVersion, ParameterVersionStatus.Published, "", DateTime.UtcNow, "test") { Parameters = values };
        var scope = WorkflowParameterScopeModel.Create(IntrinsicTimeStrategyWorkflowDefinition.Id, TimeFrameType.Daily);
        var assignment = ParameterAssignmentModel.Assign(scope, version, null, 0, DateTime.UtcNow, "test");
        var disabled = ParameterAssignmentModel.Disable(assignment, 1, DateTime.UtcNow, "test");
        var command = new AssignParameterVersionCommand
        {
            EntityId = new(assignment.AssignmentId), Scope = scope, Reference = version.Reference,
            Subject = new(ActorType.Command, AssignParameterVersionCommand.Actor, AssignParameterVersionCommand.Verb, assignment.AssignmentId.ToString("N"))
        };
        var operation = Guid.NewGuid();
        var audit = ParameterAuditModel.ForAssignment(operation, "AssignParameterVersion", "test", null, assignment);
        ParameterAssignmentChangedEvent created = legacy ? new ParameterAssignmentChangedEvent() : new ParameterAssignmentCreatedEvent();
        created = created with { EntityId = command.EntityId, CommandId = operation, Revision = 1, Assignment = assignment, RequestHash = "assign", Audit = audit };
        var disableId = Guid.NewGuid();
        var changed = new ParameterAssignmentChangedEvent { EntityId = command.EntityId, CommandId = disableId, Revision = 2,
            Assignment = disabled, RequestHash = "disable", Audit = ParameterAuditModel.ForAssignment(disableId, "DisableParameterAssignment", "test", assignment, disabled) };
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<ParameterAssignmentCommandState>().Returns(new ParameterAssignmentCommandState());
        var store = Substitute.For<IEventSourceActorDbContext>();
        store.ProviderName.Returns("System.Data.Postgres");
        store.GetEventStreamIdAsync(command.StreamId, CancellationToken.None).Returns(91L);
        store.GetCommittedStreamVersionAsync(91, CancellationToken.None).Returns(2L);
        store.MapReduceActorEventStreamFromCreationAsync<ParameterAssignmentCommandState>(91,
            Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), CancellationToken.None).Returns(call =>
        {
            call.Arg<Action<IEnumerable<EventStreamReadModel>>>()(new IEvent[] { created, changed }.Select((source, index) => new EventStreamReadModel
            {
                EventVersion = index + 1, StreamVersion = index + 1,
                EventTypeName = source.GetType().AssemblyQualifiedName!, EventData = EventLogMessagePackCodec.Shared.Serialize(source)
            }));
            return ValueTask.CompletedTask;
        });
        var repository = new ParameterAssignmentStateRepository(factory, store, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<ParameterAssignmentCommandActor>>(), NullLogger<ParameterAssignmentStateRepository>.Instance);

        var state = await repository.LoadStateAsync(command);

        Assert.Equal(disabled, state.Assignment);
        Assert.Equal(2, state.Revision);
        Assert.Equal(2, state.CommittedStreamVersion);
        Assert.Equal(command.Subject.ThreadId, state.Id);
        Assert.Equal("assign", state.Operations[operation]);
        Assert.Equal("disable", state.Operations[disableId]);
        Assert.Equal(audit, state.Audit[operation]);
        Assert.Equal(2, state.Audit.Count);
        Assert.Empty(state.Events);
        Assert.False(state.Updated);
        await store.DidNotReceiveWithAnyArgs().MapReduceActorEventStreamAsync<ParameterAssignmentCommandState>(default, default!, default);
    }
}
