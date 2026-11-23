using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.Runtime;

public sealed class WorkflowResidentStateTests
{
    [Fact]
    public async Task A_committed_owner_snapshot_supplies_continuation_state_without_a_database_read()
    {
        var source = IntrinsicTimeStrategyWorkflowCommandStateTests.CreateStartedSnapshotForQualification();
        var state = new IntrinsicTimeStrategyWorkflowCommandState();
        state.Apply(source, addEvent: false).Should().BeTrue();
        state.SetPersistedStreamVersion(1);
        state.RestoreCommittedStreamVersion(1);
        var registry = new WorkflowResidentStateRegistry();
        registry.PublishCommitted(source.EntityId.Format(), state);
        var database = Substitute.For<IDbContextFactory>();
        database.TradeDb.ProviderName.Returns("System.Data.Postgres");
        var events = Substitute.For<IEventSourceActorDbContext>();
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        var repository = new IntrinsicTimeStrategyWorkflowStateRepository(factory, events, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor>>(),
            NullLogger<IntrinsicTimeStrategyWorkflowStateRepository>.Instance, database, resident: registry);
        var command = new CompleteRegimeDiscoveryCommand
        { EntityId = source.EntityId, Subject = new(ActorType.Command, CompleteRegimeDiscoveryCommand.Actor,
            CompleteRegimeDiscoveryCommand.Verb, source.EntityId.Format()) };

        var restored = await repository.LoadStateAsync(command);

        restored.Should().NotBeSameAs(state);
        restored.CurrentView.Should().BeEquivalentTo(state.CurrentView);
        restored.PersistedStreamVersion.Should().Be(1);
        factory.ReceivedCalls().Should().BeEmpty();
        events.ReceivedCalls().Should().BeEmpty();
        // Mutating the command's fork cannot advance the committed owner.
        var next = source with { WorkflowRevision = source.WorkflowRevision + 1,
            State = source.State with { WorkflowRevision = source.WorkflowRevision + 1 } };
        restored.Apply(next, addEvent: false).Should().BeTrue();
        registry.ReadCurrent(source.EntityId.Format())!.WorkflowRevision.Should().Be(source.WorkflowRevision);
        registry.Invalidate(source.EntityId.Format());
        registry.ReadCurrent(source.EntityId.Format()).Should().BeNull();
    }

    [Fact]
    public void Uncommitted_state_cannot_be_published()
    {
        var source = IntrinsicTimeStrategyWorkflowCommandStateTests.CreateStartedSnapshotForQualification();
        var state = new IntrinsicTimeStrategyWorkflowCommandState();
        state.Apply(source, addEvent: true).Should().BeTrue();
        var registry = new WorkflowResidentStateRegistry();
        Action publish = () => registry.PublishCommitted(source.EntityId.Format(), state);
        publish.Should().Throw<InvalidOperationException>();
        registry.ReadCurrent(source.EntityId.Format()).Should().BeNull();
    }

    [Fact]
    public void A_terminal_owner_remains_hot_for_the_next_signal_in_the_same_assignment_queue()
    {
        var original = IntrinsicTimeStrategyWorkflowCommandStateTests.CreateStartedSnapshotForQualification();
        var source = original with { State = original.State with
            { Status = TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.WorkflowStrategyMachineStatus.Completed } };
        var state = new IntrinsicTimeStrategyWorkflowCommandState();
        state.Apply(source, addEvent: false).Should().BeTrue();
        state.SetPersistedStreamVersion(7); state.RestoreCommittedStreamVersion(7);
        var registry = new WorkflowResidentStateRegistry();
        registry.PublishCommitted(source.EntityId.Format(), state);

        registry.TryFork(source.EntityId.Format(), out var next).Should().BeTrue();
        next!.HasActiveWorkflow.Should().BeFalse();
        next.PersistedStreamVersion.Should().Be(7);
        next.CanAcceptStart(Guid.NewGuid()).Should().BeTrue();
        next.CurrentView.Should().BeEquivalentTo(state.CurrentView);
    }
}
