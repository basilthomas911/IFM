using FluentAssertions;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.Model;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests.DatabaseBackup;
public sealed class DatabaseBackupCommandConventionTests
{
    [Fact]
    public void Computation_does_not_mutate_state_and_factory_preserves_order_and_command_identity()
    {
        var command = Request();
        var state = new DatabaseBackupCommandState();
        command.Compute(state, out var transition).Should().BeTrue();
        state.Operation.Exists.Should().BeFalse();
        state.Operation.Revision.Should().Be(0);
        var events = command.CreateLifecycleEvents(transition);
        events.Select(sourceEvent => sourceEvent.GetType()).Should().Equal(
            typeof(DatabaseBackupRequestedDomainEvent), typeof(DatabaseBackupAuthorizedDomainEvent), typeof(DatabaseBackupExecutionRequestedDomainEvent));
        events.Should().OnlyContain(sourceEvent => sourceEvent.CommandId == command.CommandId);
        state.Update(events, command).Should().BeTrue();
        state.Operation.OperationId.Should().Be(command.EntityId);
        state.Operation.Revision.Should().Be(3);
    }
    [Fact]
    public void Revision_failure_returns_reason_without_mutating_state()
    {
        var state = new DatabaseBackupCommandState();
        var result = (Request() with { ExpectedStateRevision = 9 }).Execute(state);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Expected revision");
        state.Operation.Exists.Should().BeFalse();
    }
    [Fact]
    public void Acceptance_returns_command_identity_instead_of_operation_identity()
    {
        var command = Request();
        var result = command.Execute(new DatabaseBackupCommandState());
        result.Success.Should().BeTrue();
        result.Value!.Guid.Should().Be(command.CommandId);
        command.CommandId.Should().NotBe(command.EntityId.Value);
    }
    [Fact]
    public void Service_observation_fingerprints_remain_compatible_with_committed_events()
    {
        var command = Request();
        command.Compute(new DatabaseBackupCommandState(), out var transition).Should().BeTrue();
        var events = command.CreateLifecycleEvents(transition);
        for (var index = 0; index < events.Count; index++)
            DatabaseBackupCommandState.Fingerprint(transition.LifecycleChanges[index]).Should().Be(
                DatabaseBackupCommandState.Fingerprint(DatabaseBackupEventFactory.Describe(events[index])));
    }
    static RequestDatabaseBackupCommand Request() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = new(Guid.NewGuid()), Source = BackupSource.LocalWorkstation,
        ProtectionSetId = new("core"), Request = new() { CreatedUtc = DateTimeOffset.UtcNow }
    };
}

